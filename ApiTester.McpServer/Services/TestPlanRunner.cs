using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class TestPlanRunner
{
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
            results.Add(await RunCaseAsync(plan, tc, ct));
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

    private async Task<TestCaseResult> RunCaseAsync(TestPlan plan, TestCase tc, CancellationToken ct)
        => await RunCaseWithRetriesAsync(plan, tc, ct);

    private async Task<TestCaseResult> RunCaseWithRetriesAsync(TestPlan plan, TestCase tc, CancellationToken ct)
    {
        var maxRetries = _cfg.Policy.RetryOnFlake ? Math.Max(0, _cfg.Policy.MaxRetries) : 0;
        var attempt = 0;
        var sawFlake = false;
        string? flakeCategory = null;

        while (true)
        {
            var result = await ExecuteCaseAsync(plan, tc, ct);
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

    private async Task<TestCaseResult> ExecuteCaseAsync(TestPlan plan, TestCase tc, CancellationToken ct)
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

        var policyBlock = await PolicyBlockReasonAsync(uri, plan.Method, baseUrl, ct);
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

        var client = _httpClientFactory.CreateClient();
        client.Timeout = _cfg.Policy.Timeout;
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
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            var status = (int)resp.StatusCode;

            string? snippet = null;
            if (resp.Content is not null)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var max = Math.Min(bytes.Length, _cfg.Policy.MaxResponseBodyBytes);
                snippet = Encoding.UTF8.GetString(bytes, 0, max);
                if (bytes.Length > max) snippet += "\n... (truncated)";
            }

            var expected = tc.ExpectedStatusCodes.Count > 0 ? tc.ExpectedStatusCodes : new List<int> { 200 };
            var pass = expected.Contains(status);

            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                StatusCode = status,
                DurationMs = sw.ElapsedMilliseconds,
                Pass = pass,
                FailureReason = pass ? null : $"Expected [{string.Join(", ", expected)}] but got {status}.",
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

    private async Task<string?> PolicyBlockReasonAsync(Uri uri, string method, string baseUrl, CancellationToken ct)
    {
        var allow = _cfg.Policy.AllowedBaseUrls ?? new List<string>();
        if (allow.Count == 0 && !_cfg.Policy.DryRun)
            return "No allowedBaseUrls configured, deny by default.";

        if (_cfg.Policy.AllowedMethods is not null &&
            _cfg.Policy.AllowedMethods.Count > 0 &&
            !_cfg.Policy.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
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

        if (_cfg.Policy.BlockLocalhost || _cfg.Policy.BlockPrivateNetworks)
        {
            var (allowed, reason) = await _ssrfGuard.CheckAsync(
                uri,
                _cfg.Policy.BlockLocalhost,
                _cfg.Policy.BlockPrivateNetworks,
                ct);

            if (!allowed && !_cfg.Policy.DryRun)
                return reason ?? "Blocked by SSRF policy.";
        }

        return null;
    }

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
