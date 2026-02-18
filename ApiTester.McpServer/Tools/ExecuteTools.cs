using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class ExecuteTools
{
    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _runtime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SsrfGuard _ssrfGuard;
    private readonly ILogger<ExecuteTools> _logger;

    public ExecuteTools(
        OpenApiStore store,
        ApiRuntimeConfig runtime,
        IHttpClientFactory httpClientFactory,
        SsrfGuard ssrfGuard,
        ILogger<ExecuteTools> logger)
    {
        _store = store;
        _runtime = runtime;
        _httpClientFactory = httpClientFactory;
        _ssrfGuard = ssrfGuard;
        _logger = logger;
    }

    [McpServerTool, Description("Execute an OpenAPI operation by operationId. Optional JSON: pathParamsJson, queryParamsJson, headersJson, bodyJson.")]
    public async Task<string> ApiCallOperation(
        string operationId,
        string? pathParamsJson = null,
        string? queryParamsJson = null,
        string? headersJson = null,
        string? bodyJson = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required", nameof(operationId));

        var doc = _store.RequireDocument();

        if (!TryFindOperation(doc, operationId, out var method, out var pathTemplate))
            throw new InvalidOperationException($"OperationId not found: {operationId}");

        var baseUrl = ResolveBaseUrl(doc, _runtime.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No base URL available. Call api_set_base_url or ensure the spec contains servers[].");

        _logger.LogInformation("Executing operation {OperationId} {Method} {PathTemplate}", operationId, method, pathTemplate);

        // ---- Day 4 Step 5: Policy enforcement ----
        var policy = _runtime.Policy;

        // A) Method allowlist
        if (!policy.AllowedMethods.Contains(method))
        {
            _logger.LogWarning(
                "Blocked operation {OperationId} due to disallowed method {Method}",
                operationId,
                method);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = $"Method not allowed by policy: {method}",
                operationId,
                method,
                baseUrl = baseUrl.Trim().TrimEnd('/'),
                url = (string?)null
            }, JsonDefaults.Default);
        }


        // B) Base URL allowlist (simple prefix match for now, harden later)
        var normalisedBaseUrl = baseUrl.Trim().TrimEnd('/');

        if (policy.HostedMode && policy.AllowedBaseUrls.Count == 0)
        {
            _logger.LogWarning("Blocked operation {OperationId} in hosted mode due to empty allowed base URLs", operationId);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = "Hosted mode requires at least one allowedBaseUrls entry. Deny by default.",
                operationId,
                method,
                baseUrl = normalisedBaseUrl,
                url = (string?)null
            }, JsonDefaults.Default);
        }

        if (policy.AllowedBaseUrls.Count == 0 && !policy.DryRun)
        {
            _logger.LogWarning("Blocked operation {OperationId} due to empty allowed base URLs", operationId);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = "No allowedBaseUrls configured, deny by default.",
                operationId,
                method,
                baseUrl = normalisedBaseUrl,
                url = (string?)null
            }, JsonDefaults.Default);
        }

        if (policy.AllowedBaseUrls.Count > 0 &&
            !policy.AllowedBaseUrls.Any(allowed =>
                normalisedBaseUrl.StartsWith(allowed.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "Blocked operation {OperationId} due to base URL {BaseUrl} not in allow list",
                operationId,
                normalisedBaseUrl);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = $"Base URL not allowed by policy: {normalisedBaseUrl}",
                operationId,
                method,
                baseUrl = normalisedBaseUrl,
                url = (string?)null
            }, JsonDefaults.Default);
        }

        // -----------------------------------------

        var pathParams = ParseObject(pathParamsJson);
        var queryParams = ParseObject(queryParamsJson);
        var headers = ParseObject(headersJson);

        var path = ApplyPathParams(pathTemplate, pathParams);
        var url = BuildUrl(baseUrl, path, queryParams);
        var uri = new Uri(url);

        var (allowed, reason) = await _ssrfGuard.CheckAsync(
            uri,
            blockLocalhost: policy.BlockLocalhost,
            blockPrivateNetworks: policy.BlockPrivateNetworks,
            ct: ct);

        if (!allowed && !policy.DryRun)
        {
            _logger.LogWarning("Blocked operation {OperationId} due to SSRF policy: {Reason}", operationId, reason);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason,
                operationId,
                method,
                url
            }, JsonDefaults.Default);
        }


        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var kvp in headers)
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);

        if (!request.Headers.Contains("Authorization") && !string.IsNullOrWhiteSpace(_runtime.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _runtime.BearerToken);

        if (!string.IsNullOrWhiteSpace(bodyJson) &&
            (method is "POST" or "PUT" or "PATCH"))
        {
            // Optional extra guard: basic request size limit
            // (uses UTF-8 byte count, policy value is bytes)
            var bodyBytes = Encoding.UTF8.GetByteCount(bodyJson);
            if (bodyBytes > policy.MaxRequestBodyBytes)
                throw new InvalidOperationException($"Request body exceeds MaxRequestBodyBytes ({policy.MaxRequestBodyBytes}).");

            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        // C) Dry run mode (build request, do not send)
        if (policy.DryRun)
        {
            _logger.LogInformation("Dry run for operation {OperationId} {Method} {Url}", operationId, method, url);
            var dryRunResult = new
            {
                dryRun = true,
                operationId,
                method,
                path = pathTemplate,
                url,
                // Helpful for debugging, shows what would be sent
                requestHeaders = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                hasBody = request.Content != null,
                bodyPreview = request.Content == null
                    ? null
                    : (bodyJson!.Length > 2000 ? bodyJson[..2000] + "\n... (truncated)" : bodyJson)
            };

            return JsonSerializer.Serialize(dryRunResult, JsonDefaults.Default);
        }

        var client = _httpClientFactory.CreateClient(TestPlanRunner.HttpClientName);
        client.Timeout = policy.Timeout;

        var sw = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        sw.Stop();

        var (responseBody, truncated) = await ReadBodyCappedAsync(response.Content, policy.MaxResponseBodyBytes, ct);

        if (truncated)
            responseBody += "\n... (truncated)";


        // Existing char cap kept, but also add byte cap aligned with policy
        // (simple approach: truncate string if UTF-8 byte length exceeds policy max)
        var responseBytes = Encoding.UTF8.GetByteCount(responseBody);
        if (responseBytes > policy.MaxResponseBodyBytes)
        {
            // crude truncation by chars; ok for now, harden later with stream reading
            const int hardCharLimit = 50_000;
            responseBody = responseBody.Length > hardCharLimit
                ? responseBody[..hardCharLimit] + "\n... (truncated)"
                : responseBody + "\n... (truncated)";
        }
        else
        {
            const int maxBodyChars = 50_000;
            if (responseBody.Length > maxBodyChars)
                responseBody = responseBody[..maxBodyChars] + "\n... (truncated)";
        }

        var result = new
        {
            operationId,
            method,
            path = pathTemplate,
            url,
            statusCode = (int)response.StatusCode,
            reasonPhrase = response.ReasonPhrase,
            durationMs = sw.ElapsedMilliseconds,
            responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            contentHeaders = response.Content.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            body = responseBody
        };

        _logger.LogInformation(
            "Executed operation {OperationId} {Method} {StatusCode} in {DurationMs}ms",
            operationId,
            method,
            (int)response.StatusCode,
            sw.ElapsedMilliseconds);

        return JsonSerializer.Serialize(result, JsonDefaults.Default);
    }

    private static async Task<(string Text, bool Truncated)> ReadBodyCappedAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();

        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read <= 0) break;

            var remaining = maxBytes - total;
            if (remaining <= 0) return (Encoding.UTF8.GetString(ms.ToArray()), true);

            var toWrite = Math.Min(read, remaining);
            ms.Write(buffer, 0, toWrite);
            total += toWrite;

            if (toWrite < read) // hit cap
                return (Encoding.UTF8.GetString(ms.ToArray()), true);
        }

        return (Encoding.UTF8.GetString(ms.ToArray()), false);
    }


    private static bool TryFindOperation(OpenApiDocument doc, string operationId, out string method, out string path)
    {
        foreach (var p in doc.Paths)
        {
            foreach (var o in p.Value.Operations)
            {
                if (string.Equals(o.Value.OperationId, operationId, StringComparison.Ordinal))
                {
                    method = o.Key.ToString().ToUpperInvariant();
                    path = p.Key;
                    return true;
                }
            }
        }

        method = "";
        path = "";
        return false;
    }

    private static string ResolveBaseUrl(OpenApiDocument doc, string? runtimeBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBaseUrl))
            return runtimeBaseUrl.Trim().TrimEnd('/');

        var specUrl = doc.Servers?.FirstOrDefault()?.Url?.Trim();
        return string.IsNullOrWhiteSpace(specUrl) ? "" : specUrl.TrimEnd('/');
    }

    private static Dictionary<string, string> ParseObject(string? json)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
            return dict;

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Expected a JSON object.");

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? ""
                : prop.Value.ToString();
        }

        return dict;
    }

    private static string ApplyPathParams(string pathTemplate, Dictionary<string, string> pathParams)
    {
        var path = pathTemplate;

        foreach (var kvp in pathParams)
        {
            path = path.Replace("{" + kvp.Key + "}", Uri.EscapeDataString(kvp.Value), StringComparison.OrdinalIgnoreCase);
        }

        if (path.Contains('{') || path.Contains('}'))
            throw new InvalidOperationException($"Missing required path params for path: {pathTemplate}");

        return path;
    }

    private static string BuildUrl(string baseUrl, string path, Dictionary<string, string> queryParams)
    {
        baseUrl = baseUrl.TrimEnd('/');
        path = path.StartsWith('/') ? path : "/" + path;

        var sb = new StringBuilder();
        sb.Append(baseUrl).Append(path);

        if (queryParams.Count > 0)
        {
            sb.Append('?');
            sb.Append(string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")));
        }

        return sb.ToString();
    }
}
