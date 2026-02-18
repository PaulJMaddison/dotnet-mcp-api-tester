using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class TestPlanRunner
{
    public const string HttpClientName = "TestPlanRunner";

    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _cfg;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITestRunStore _runStore;
    private readonly SsrfGuard _ssrfGuard;
    private readonly ILogger<TestPlanRunner> _logger;

    public TestPlanRunner(
        OpenApiStore store,
        ApiRuntimeConfig cfg,
        IHttpClientFactory httpClientFactory,
        ITestRunStore runStore,
        SsrfGuard ssrfGuard,
        ILogger<TestPlanRunner> logger)
    {
        _store = store;
        _cfg = cfg;
        _httpClientFactory = httpClientFactory;
        _runStore = runStore;
        _ssrfGuard = ssrfGuard;
        _logger = logger;
    }

    // Backwards compatible entry point
    public Task<TestRunRecord> RunAsync(string operationId, CancellationToken ct = default)
        => RunAsync(operationId, "default", ct);

    // Day 14: SaaS scoping via projectKey
    public async Task<TestRunRecord> RunAsync(string operationId, string projectKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        var doc = _store.RequireDocument();
        var match = FindOperation(doc, operationId);

        if (match is null)
            throw new InvalidOperationException($"operationId not found in OpenAPI: {operationId}");

        var (path, method, op) = match.Value;

        var plan = TestPlanFactory.Create(op, method, path, operationId);
        return await RunPlanAsync(plan, projectKey, ct);
    }

    public async Task<TestRunRecord> RunPlanAsync(TestPlan plan, string projectKey, CancellationToken ct = default)
        => await RunPlanAsync(plan, projectKey, OrgDefaults.DefaultOrganisationId, OwnerKeyDefaults.Default, null, null, null, ct);

    public async Task<TestRunRecord> RunPlanAsync(
        TestPlan plan,
        string projectKey,
        Guid organisationId,
        string ownerKey,
        Guid? specId,
        string? actor,
        string? environmentName,
        CancellationToken ct = default)
        => await RunPlanInternalAsync(plan, projectKey, organisationId, ownerKey, specId, actor, environmentName, ct);

    public async Task<TestRunRecord> RunPlanAsync(
        TestPlan plan,
        string projectKey,
        string ownerKey,
        Guid? specId,
        string? actor,
        string? environmentName,
        CancellationToken ct = default)
        => await RunPlanInternalAsync(plan, projectKey, OrgDefaults.DefaultOrganisationId, ownerKey, specId, actor, environmentName, ct);

    private async Task<TestRunRecord> RunPlanInternalAsync(
        TestPlan plan,
        string projectKey,
        Guid organisationId,
        string ownerKey,
        Guid? specId,
        string? actor,
        string? environmentName,
        CancellationToken ct)
    {
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));

        if (string.IsNullOrWhiteSpace(plan.OperationId))
            throw new InvalidOperationException("Test plan missing operationId.");

        organisationId = organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        var startedUtc = DateTimeOffset.UtcNow;
        var auditActor = string.IsNullOrWhiteSpace(actor) ? ownerKey : actor.Trim();
        var auditEnvironment = string.IsNullOrWhiteSpace(environmentName) ? null : environmentName.Trim();
        var environmentSnapshot = new TestRunEnvironmentSnapshot(auditEnvironment, _cfg.BaseUrl);
        var policySnapshot = ApiExecutionPolicySnapshot.FromPolicy(_cfg.Policy);

        _logger.LogInformation(
            "Executing test plan {OperationId} for org {OrganisationId} owner {OwnerKey} project {ProjectKey} with {CaseCount} cases",
            plan.OperationId,
            organisationId,
            ownerKey,
            projectKey,
            plan.Cases.Count);

        var swTotal = Stopwatch.StartNew();
        var results = new List<TestCaseResult>();

        foreach (var tc in plan.Cases)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await RunCaseAsync(plan, tc, policySnapshot, ct));
        }

        swTotal.Stop();

        var blocked = results.Count(x => x.Blocked);
        var passed = results.Count(x => !x.Blocked && x.Pass);
        var failed = results.Count(x => !x.Blocked && !x.Pass);
        var classificationSummary = ResultClassificationRules.Summarize(results);

        var completedUtc = DateTimeOffset.UtcNow;

        var result = new TestRunResult
        {
            OperationId = plan.OperationId.Trim(),
            TotalCases = results.Count,
            Passed = passed,
            Failed = failed,
            Blocked = blocked,
            TotalDurationMs = swTotal.ElapsedMilliseconds,
            ClassificationSummary = classificationSummary,
            Results = results
        };

        var record = new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OrganisationId = organisationId,
            TenantId = organisationId,
            Actor = auditActor,
            Environment = environmentSnapshot,
            PolicySnapshot = policySnapshot,
            OwnerKey = ownerKey,
            ProjectKey = projectKey,
            OperationId = plan.OperationId.Trim(),
            SpecId = specId,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            Result = result
        };

        // Day 14 store interface: SaveAsync(record) (no CancellationToken)
        await _runStore.SaveAsync(record);

        _logger.LogInformation(
            "Stored test run {RunId} for operation {OperationId} in {DurationMs}ms (passed {Passed}, failed {Failed}, blocked {Blocked})",
            record.RunId,
            record.OperationId,
            swTotal.ElapsedMilliseconds,
            result.Passed,
            result.Failed,
            result.Blocked);

        return record;
    }

    private async Task<TestCaseResult> RunCaseAsync(TestPlan plan, TestCase tc, ApiExecutionPolicySnapshot policySnapshot, CancellationToken ct)
        => await RunCaseWithRetriesAsync(plan, tc, policySnapshot, ct);

    private async Task<TestCaseResult> RunCaseWithRetriesAsync(TestPlan plan, TestCase tc, ApiExecutionPolicySnapshot policySnapshot, CancellationToken ct)
    {
        var maxRetries = policySnapshot.RetryOnFlake ? Math.Max(0, policySnapshot.MaxRetries) : 0;
        var attempt = 0;
        var sawFlake = false;
        string? flakeCategory = null;

        while (true)
        {
            var result = await ExecuteCaseAsync(plan, tc, policySnapshot, ct);
            var classification = ResultClassificationRules.Classify(result);

            if (classification == ResultClassification.FlakyExternal)
            {
                sawFlake = true;
                flakeCategory ??= result.FlakeReasonCategory;

                if (attempt < maxRetries)
                {
                    attempt++;
                    continue;
                }
            }

            if (sawFlake && result.Pass)
            {
                result.IsFlaky = true;
                result.FlakeReasonCategory ??= flakeCategory;
                result.Classification = ResultClassification.FlakyExternal;
            }

            return result;
        }
    }

    private async Task<TestCaseResult> ExecuteCaseAsync(TestPlan plan, TestCase tc, ApiExecutionPolicySnapshot policySnapshot, CancellationToken ct)
    {
        var missing = ExtractPathParamNames(plan.PathTemplate)
            .Where(p => !tc.PathParams.ContainsKey(p))
            .ToList();

        if (missing.Count > 0)
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = $"Missing required path param(s): {string.Join(", ", missing)}",
                Method = plan.Method,
                Pass = false
            };
        }

        var baseUrl = (_cfg.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = "No baseUrl configured. Call api_set_base_url first.",
                Method = plan.Method,
                Pass = false
            };
        }

        var path = ApplyPathParams(plan.PathTemplate, tc.PathParams);
        var full = baseUrl + path;

        if (tc.QueryParams.Count > 0)
        {
            var qs = string.Join("&", tc.QueryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            full += "?" + qs;
        }

        if (!Uri.TryCreate(full, UriKind.Absolute, out var uri))
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = $"Built URL is invalid: {full}",
                Method = plan.Method,
                Pass = false
            };
        }

        var policyBlock = await PolicyBlockReasonAsync(uri, plan.Method, baseUrl, policySnapshot, ct);
        if (policyBlock is not null)
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = policyBlock,
                Method = plan.Method,
                Url = uri.ToString(),
                Pass = false
            };
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeout = TimeSpan.FromSeconds(policySnapshot.TimeoutSeconds);
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCts.CancelAfter(timeout);
        }

        var requestToken = timeoutCts.Token;
        using var req = new HttpRequestMessage(new HttpMethod(plan.Method), uri);

        if (!string.IsNullOrWhiteSpace(_cfg.BearerToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.BearerToken);

        foreach (var h in tc.Headers)
            req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (!string.IsNullOrWhiteSpace(tc.BodyJson))
        {
            req.Content = new StringContent(tc.BodyJson, Encoding.UTF8, "application/json");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await SendWithRedirectPolicyAsync(client, req, policySnapshot, requestToken);
            sw.Stop();

            var status = (int)resp.StatusCode;

            string? snippet = null;
            byte[] bodyBytes = Array.Empty<byte>();
            if (resp.Content is not null)
            {
                bodyBytes = await resp.Content.ReadAsByteArrayAsync(requestToken);
                var max = Math.Min(bodyBytes.Length, policySnapshot.MaxResponseBodyBytes);
                snippet = Encoding.UTF8.GetString(bodyBytes, 0, max);
                if (bodyBytes.Length > max) snippet += "\n... (truncated)";
            }

            var expected = tc.ExpectedStatusCodes.Count > 0 ? tc.ExpectedStatusCodes : new List<int> { 200 };
            var pass = expected.Contains(status);
            var failureReason = pass ? null : $"Expected [{string.Join(", ", expected)}] but got {status}.";
            object? failureDetails = null;
            string? validationUnavailableReason = null;

            if (pass && policySnapshot.ValidateSchema)
            {
                var contractValidation = ValidateContract(plan, resp, bodyBytes, status);
                if (contractValidation is not null)
                {
                    if (contractValidation.IsViolation)
                    {
                        pass = false;
                        failureReason = FailureReasons.ContractViolation;
                        failureDetails = contractValidation.Details;
                    }
                    else if (!string.IsNullOrWhiteSpace(contractValidation.UnavailableReason))
                    {
                        validationUnavailableReason = contractValidation.UnavailableReason;
                    }
                }
            }

            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                StatusCode = status,
                DurationMs = sw.ElapsedMilliseconds,
                Pass = pass,
                FailureReason = failureReason,
                FailureDetails = failureDetails,
                ValidationUnavailableReason = validationUnavailableReason,
                ResponseSnippet = snippet
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                Pass = false,
                FailureReason = "Request timed out."
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Redirect blocked by policy:", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = ex.Message,
                Method = plan.Method,
                Url = uri.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                Pass = false
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            var failureReason = ex.InnerException is System.Net.Sockets.SocketException socketException &&
                                socketException.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound
                ? "DNS lookup failed."
                : ex.Message;

            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                Pass = false,
                FailureReason = failureReason
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                Pass = false,
                FailureReason = ex.Message
            };
        }
    }

    private ContractValidationResult? ValidateContract(TestPlan plan, HttpResponseMessage response, byte[] bodyBytes, int statusCode)
    {
        var doc = _store.Document;
        if (doc is null)
            return ContractValidationResult.Unavailable("No OpenAPI document loaded.");

        var operation = ResolveOperation(plan, doc);
        if (operation is null)
            return ContractValidationResult.Unavailable($"Operation '{plan.OperationId}' was not found in OpenAPI document.");

        if (!TryGetResponse(operation, statusCode, out var expectedResponse) || expectedResponse is null)
            return ContractValidationResult.Unavailable($"No OpenAPI response schema matched status code {statusCode}.");

        if (expectedResponse.Content is null || expectedResponse.Content.Count == 0)
            return ContractValidationResult.Unavailable("OpenAPI response does not define content schemas.");

        var actualContentType = response.Content?.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(actualContentType))
        {
            return ContractValidationResult.Violation(BuildContractFailure(
                "content_type_missing",
                "Response content type was missing but schema defines response content.",
                new { expected = expectedResponse.Content.Keys.ToArray(), status = statusCode }));
        }

        var matchedContentType = expectedResponse.Content.Keys
            .FirstOrDefault(key => ContentTypeMatches(key, actualContentType));

        if (matchedContentType is null)
        {
            return ContractValidationResult.Violation(BuildContractFailure(
                "content_type_mismatch",
                "Response content type did not match OpenAPI schema.",
                new { expected = expectedResponse.Content.Keys.ToArray(), actual = actualContentType, status = statusCode }));
        }

        if (!IsJsonContentType(matchedContentType))
            return ContractValidationResult.Unavailable($"Schema validation is only supported for application/json content. Matched '{matchedContentType}'.");

        var schema = ResolveSchema(doc, expectedResponse.Content[matchedContentType].Schema);
        if (schema is null)
            return ContractValidationResult.Unavailable("OpenAPI response schema was not available.");

        if (bodyBytes.Length == 0)
            return ContractValidationResult.Unavailable("Response body was empty, skipping JSON schema validation.");

        JsonDocument docBody;
        try
        {
            docBody = JsonDocument.Parse(bodyBytes);
        }
        catch (JsonException ex)
        {
            return ContractValidationResult.Violation(BuildContractFailure(
                "invalid_json",
                "Response body was not valid JSON.",
                new { error = ex.Message, status = statusCode, contentType = actualContentType }));
        }

        using (docBody)
        {
            var errors = new List<string>();
            ValidateSchema(doc, schema, docBody.RootElement, "$", errors, new HashSet<OpenApiSchema>());

            if (errors.Count > 0)
            {
                return ContractValidationResult.Violation(BuildContractFailure(
                    "schema_mismatch",
                    "Response body did not match OpenAPI schema.",
                    new { errors, status = statusCode, contentType = actualContentType }));
            }
        }

        return null;
    }

    private static OpenApiOperation? ResolveOperation(TestPlan plan, OpenApiDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(plan.OperationId))
        {
            var match = FindOperation(doc, plan.OperationId);
            if (match.HasValue)
                return match.Value.op;
        }

        if (Enum.TryParse<OperationType>(plan.Method, true, out var operationType) &&
            doc.Paths.TryGetValue(plan.PathTemplate, out var pathItem) &&
            pathItem.Operations.TryGetValue(operationType, out var operation))
        {
            return operation;
        }

        return null;
    }

    private static bool TryGetResponse(OpenApiOperation operation, int statusCode, out OpenApiResponse? response)
    {
        response = null;
        if (operation.Responses is null)
            return false;

        var code = statusCode.ToString();
        if (operation.Responses.TryGetValue(code, out response))
            return true;

        if (operation.Responses.TryGetValue("default", out response))
            return true;

        return false;
    }

    private static bool ContentTypeMatches(string expected, string actual)
    {
        if (string.Equals(expected, "*/*", StringComparison.OrdinalIgnoreCase))
            return true;

        if (expected.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = expected[..expected.IndexOf('/')];
            return actual.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJsonContentType(string mediaType)
        => mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
           mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    private static OpenApiSchema? ResolveSchema(OpenApiDocument doc, OpenApiSchema? schema, HashSet<OpenApiSchema>? visited = null)
    {
        if (schema is null)
            return null;

        visited ??= new HashSet<OpenApiSchema>();

        if (!visited.Add(schema))
            return schema;

        var referenceId = schema.Reference?.Id;
        if (!string.IsNullOrWhiteSpace(referenceId) &&
            doc.Components?.Schemas is not null &&
            doc.Components.Schemas.TryGetValue(referenceId, out var resolved))
        {
            return ResolveSchema(doc, resolved, visited);
        }

        return schema;
    }

    private static void ValidateSchema(
        OpenApiDocument doc,
        OpenApiSchema schema,
        JsonElement element,
        string path,
        List<string> errors,
        HashSet<OpenApiSchema> visited)
    {
        schema = ResolveSchema(doc, schema, visited) ?? schema;

        if (schema.Nullable && element.ValueKind == JsonValueKind.Null)
            return;

        var expectedType = schema.Type;
        if (string.IsNullOrWhiteSpace(expectedType))
        {
            if (schema.Properties?.Count > 0)
                expectedType = "object";
            else if (schema.Items is not null)
                expectedType = "array";
        }

        switch (expectedType?.ToLowerInvariant())
        {
            case "object":
                if (element.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"{path}: expected object.");
                    return;
                }

                var required = schema.Required ?? new HashSet<string>();
                foreach (var property in required)
                {
                    if (!element.TryGetProperty(property, out _))
                        errors.Add($"{path}: missing required property '{property}'.");
                }

                if (schema.Properties is not null)
                {
                    foreach (var prop in schema.Properties)
                    {
                        if (element.TryGetProperty(prop.Key, out var value))
                        {
                            ValidateSchema(doc, prop.Value, value, $"{path}.{prop.Key}", errors, visited);
                        }
                    }
                }
                break;
            case "array":
                if (element.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{path}: expected array.");
                    return;
                }

                if (schema.Items is not null)
                {
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        ValidateSchema(doc, schema.Items, item, $"{path}[{index}]", errors, visited);
                        index++;
                    }
                }
                break;
            case "string":
                if (element.ValueKind != JsonValueKind.String)
                    errors.Add($"{path}: expected string.");
                break;
            case "integer":
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out _))
                    errors.Add($"{path}: expected integer.");
                break;
            case "number":
                if (element.ValueKind != JsonValueKind.Number)
                    errors.Add($"{path}: expected number.");
                break;
            case "boolean":
                if (element.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    errors.Add($"{path}: expected boolean.");
                break;
            default:
                break;
        }
    }

    private static ContractFailureReason BuildContractFailure(string type, string message, object? details)
        => new("contract", type, message, details);

    private sealed record ContractValidationResult(bool IsViolation, ContractFailureReason? Details, string? UnavailableReason)
    {
        public static ContractValidationResult Violation(ContractFailureReason details) => new(true, details, null);
        public static ContractValidationResult Unavailable(string reason) => new(false, null, reason);
    }

    private async Task<string?> PolicyBlockReasonAsync(Uri uri, string method, string baseUrl, ApiExecutionPolicySnapshot policySnapshot, CancellationToken ct)
    {
        var allow = policySnapshot.AllowedBaseUrls ?? [];
        if (policySnapshot.HostedMode && allow.Count == 0)
            return "Hosted mode requires at least one allowedBaseUrl. Deny by default.";

        if (allow.Count == 0 && !policySnapshot.DryRun)
            return "No allowedBaseUrls configured, deny by default.";

        if (policySnapshot.AllowedMethods is not null &&
            policySnapshot.AllowedMethods.Count > 0 &&
            !policySnapshot.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return $"HTTP method not allowed by policy: {method}";
        }

        if (allow.Count > 0)
        {
            var u = baseUrl.Trim().TrimEnd('/');
            var ok = allow.Any(a =>
            {
                var aa = (a ?? "").Trim().TrimEnd('/');
                return u.StartsWith(aa, StringComparison.OrdinalIgnoreCase);
            });

            if (!ok)
                return $"Base URL not allowed by policy: {u}";
        }

        if (policySnapshot.BlockLocalhost || policySnapshot.BlockPrivateNetworks)
        {
            var (allowed, reason) = await _ssrfGuard.CheckAsync(
                uri,
                policySnapshot.BlockLocalhost,
                policySnapshot.BlockPrivateNetworks,
                ct);

            if (!allowed && !policySnapshot.DryRun)
                return reason ?? "Blocked by SSRF policy.";
        }

        return null;
    }

    private async Task<HttpResponseMessage> SendWithRedirectPolicyAsync(HttpClient client, HttpRequestMessage request, ApiExecutionPolicySnapshot policySnapshot, CancellationToken ct)
    {
        const int maxRedirectHops = 5;

        var currentUri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
        var visitedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            currentUri.AbsoluteUri
        };

        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        for (var hop = 0; hop <= maxRedirectHops; hop++)
        {
            using var currentRequest = new HttpRequestMessage(request.Method, currentUri);
            foreach (var header in request.Headers)
                currentRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (body is not null)
                currentRequest.Content = new StringContent(body, Encoding.UTF8, request.Content?.Headers.ContentType?.MediaType ?? "application/json");

            var response = await client.SendAsync(currentRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!IsRedirectStatusCode((int)response.StatusCode) || response.Headers.Location is null)
                return response;

            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(currentUri, response.Headers.Location);

            var redirectBlock = await PolicyBlockReasonAsync(
                redirectUri,
                request.Method.Method,
                $"{redirectUri.Scheme}://{redirectUri.Authority}",
                policySnapshot,
                ct);

            if (redirectBlock is not null)
            {
                response.Dispose();
                throw new InvalidOperationException($"Redirect blocked by policy: {redirectBlock}");
            }

            if (!visitedUris.Add(redirectUri.AbsoluteUri))
            {
                response.Dispose();
                throw new InvalidOperationException("Redirect blocked by policy: Redirect loop detected.");
            }

            if (hop == maxRedirectHops)
            {
                response.Dispose();
                throw new InvalidOperationException($"Redirect blocked by policy: Redirect limit of {maxRedirectHops} exceeded.");
            }

            response.Dispose();
            currentUri = redirectUri;
        }

        throw new InvalidOperationException($"Redirect blocked by policy: Redirect limit of {maxRedirectHops} exceeded.");
    }

    private static bool IsRedirectStatusCode(int statusCode)
        => statusCode is 301 or 302 or 303 or 307 or 308;

    private static (string path, OperationType method, OpenApiOperation op)? FindOperation(OpenApiDocument doc, string operationId)
    {
        foreach (var path in doc.Paths)
        {
            foreach (var kv in path.Value.Operations)
            {
                var op = kv.Value;
                if (string.Equals(op.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                    return (path.Key, kv.Key, op);
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractPathParamNames(string pathTemplate)
    {
        var names = new List<string>();
        var i = 0;
        while (i < pathTemplate.Length)
        {
            var start = pathTemplate.IndexOf('{', i);
            if (start < 0) break;
            var end = pathTemplate.IndexOf('}', start + 1);
            if (end < 0) break;

            var name = pathTemplate.Substring(start + 1, end - start - 1);
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);

            i = end + 1;
        }

        return names;
    }

    private static string ApplyPathParams(string pathTemplate, Dictionary<string, string> pathParams)
    {
        var result = pathTemplate;
        foreach (var kv in pathParams)
        {
            result = result.Replace("{" + kv.Key + "}", Uri.EscapeDataString(kv.Value), StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
