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
    public const string HttpClientName = "ApiExecution";

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

    [McpServerTool, Description("Build or execute an OpenAPI operation by operationId using this process's safety policy. Optional JSON: pathParamsJson, queryParamsJson, headersJson, bodyJson.")]
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

        var document = _store.RequireDocument();
        var match = OpenApiOperationIdentity.Find(document, operationId.Trim())
            ?? throw new InvalidOperationException($"OperationId not found: {operationId}");

        var method = match.Method.ToString().ToUpperInvariant();
        var pathTemplate = match.Path;
        var baseUrl = ResolveBaseUrl(document, _runtime.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No base URL available. Call api_set_base_url or ensure the spec contains servers[].");

        var policy = _runtime.Policy;
        var normalisedBaseUrl = baseUrl.Trim().TrimEnd('/');

        if (!policy.AllowedMethods.Contains(method))
            return Blocked($"Method not allowed by policy: {method}", operationId, method, normalisedBaseUrl);

        if (policy.HostedMode && policy.AllowedBaseUrls.Count == 0)
            return Blocked("Hosted mode requires at least one allowedBaseUrls entry. Deny by default.", operationId, method, normalisedBaseUrl);

        if (policy.AllowedBaseUrls.Count == 0 && !policy.DryRun)
            return Blocked("No allowedBaseUrls configured, deny by default.", operationId, method, normalisedBaseUrl);

        if (policy.AllowedBaseUrls.Count > 0 &&
            !policy.AllowedBaseUrls.Any(allowed => IsAllowedBaseUrl(normalisedBaseUrl, allowed)))
        {
            return Blocked($"Base URL not allowed by policy: {normalisedBaseUrl}", operationId, method, normalisedBaseUrl);
        }

        var pathParams = ParseObject(pathParamsJson);
        var queryParams = ParseObject(queryParamsJson);
        var headers = ParseObject(headersJson);
        var path = ApplyPathParams(pathTemplate, pathParams);
        var url = BuildUrl(baseUrl, path, queryParams);
        var uri = new Uri(url);

        var (allowedBySsrf, reason) = await _ssrfGuard.CheckAsync(
            uri,
            policy.BlockLocalhost,
            policy.BlockPrivateNetworks,
            ct);

        if (!allowedBySsrf && !policy.DryRun)
            return Blocked(reason ?? "Blocked by network safety policy.", operationId, method, normalisedBaseUrl, url);

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var pair in headers)
            request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);

        if (!request.Headers.Contains("Authorization") && !string.IsNullOrWhiteSpace(_runtime.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _runtime.BearerToken);

        if (!string.IsNullOrWhiteSpace(bodyJson) && method is "POST" or "PUT" or "PATCH")
        {
            var bodyBytes = Encoding.UTF8.GetByteCount(bodyJson);
            if (bodyBytes > policy.MaxRequestBodyBytes)
                throw new InvalidOperationException($"Request body exceeds MaxRequestBodyBytes ({policy.MaxRequestBodyBytes}).");
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        if (policy.DryRun)
        {
            return JsonSerializer.Serialize(new
            {
                dryRun = true,
                operationId = match.OperationId,
                method,
                path = pathTemplate,
                url,
                requestHeaders = RedactHeaders(request.Headers),
                hasBody = request.Content is not null,
                bodyPreview = request.Content is null ? null : Preview(bodyJson!, 2000)
            }, JsonDefaults.Default);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = policy.Timeout;

        _logger.LogInformation("Executing operation {OperationId} {Method} {Url}", match.OperationId, method, url);
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        stopwatch.Stop();

        var (responseBody, truncated) = await ReadBodyCappedAsync(response.Content, policy.MaxResponseBodyBytes, ct);
        if (truncated) responseBody += "\n... (truncated)";

        return JsonSerializer.Serialize(new
        {
            operationId = match.OperationId,
            method,
            path = pathTemplate,
            url,
            statusCode = (int)response.StatusCode,
            reasonPhrase = response.ReasonPhrase,
            durationMs = stopwatch.ElapsedMilliseconds,
            responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            contentHeaders = response.Content.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            body = responseBody
        }, JsonDefaults.Default);
    }

    private static string Blocked(string reason, string operationId, string method, string baseUrl, string? url = null)
        => JsonSerializer.Serialize(new { blocked = true, reason, operationId, method, baseUrl, url }, JsonDefaults.Default);

    private static bool IsAllowedBaseUrl(string actual, string configured)
    {
        if (!Uri.TryCreate(actual, UriKind.Absolute, out var actualUri) ||
            !Uri.TryCreate(configured.Trim().TrimEnd('/'), UriKind.Absolute, out var allowedUri))
            return false;

        if (!string.Equals(actualUri.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actualUri.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) ||
            actualUri.Port != allowedUri.Port)
            return false;

        var allowedPath = allowedUri.AbsolutePath.TrimEnd('/');
        if (allowedPath.Length == 0) return true;
        var actualPath = actualUri.AbsolutePath.TrimEnd('/');
        return actualPath.Equals(allowedPath, StringComparison.Ordinal) ||
               actualPath.StartsWith(allowedPath + "/", StringComparison.Ordinal);
    }

    private static async Task<(string Text, bool Truncated)> ReadBodyCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0) break;
            var remaining = maxBytes - total;
            if (remaining <= 0) return (Encoding.UTF8.GetString(ms.ToArray()), true);
            var toWrite = Math.Min(read, remaining);
            ms.Write(buffer, 0, toWrite);
            total += toWrite;
            if (toWrite < read) return (Encoding.UTF8.GetString(ms.ToArray()), true);
        }

        return (Encoding.UTF8.GetString(ms.ToArray()), false);
    }

    private static string ResolveBaseUrl(OpenApiDocument document, string? runtimeBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBaseUrl))
            return runtimeBaseUrl.Trim().TrimEnd('/');
        var fromSpec = document.Servers?.FirstOrDefault()?.Url?.Trim();
        return string.IsNullOrWhiteSpace(fromSpec) ? string.Empty : fromSpec.TrimEnd('/');
    }

    private static Dictionary<string, string> ParseObject(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Expected a JSON object.");

        foreach (var property in document.RootElement.EnumerateObject())
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
        return result;
    }

    private static string ApplyPathParams(string pathTemplate, Dictionary<string, string> pathParams)
    {
        var path = pathTemplate;
        foreach (var pair in pathParams)
            path = path.Replace("{" + pair.Key + "}", Uri.EscapeDataString(pair.Value), StringComparison.OrdinalIgnoreCase);
        if (path.Contains('{') || path.Contains('}'))
            throw new InvalidOperationException($"Missing required path params for path: {pathTemplate}");
        return path;
    }

    private static string BuildUrl(string baseUrl, string path, Dictionary<string, string> queryParams)
    {
        var builder = new StringBuilder(baseUrl.TrimEnd('/'));
        builder.Append(path.StartsWith('/') ? path : "/" + path);
        if (queryParams.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join("&", queryParams.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
        }
        return builder.ToString();
    }

    private static Dictionary<string, string> RedactHeaders(HttpRequestHeaders headers)
        => headers.ToDictionary(
            header => header.Key,
            header => header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? "[redacted]"
                : string.Join(", ", header.Value),
            StringComparer.OrdinalIgnoreCase);

    private static string Preview(string value, int maxChars)
        => value.Length <= maxChars ? value : value[..maxChars] + "\n... (truncated)";
}
