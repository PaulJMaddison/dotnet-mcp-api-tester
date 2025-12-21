using ApiTester.McpServer.Services;
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

    public ExecuteTools(OpenApiStore store, ApiRuntimeConfig runtime, IHttpClientFactory httpClientFactory)
    {
        _store = store;
        _runtime = runtime;
        _httpClientFactory = httpClientFactory;
    }

    [McpServerTool, Description("Execute an OpenAPI operation by operationId. Optional JSON: pathParamsJson, queryParamsJson, headersJson, bodyJson.")]
    public async Task<string> ApiCallOperation(
        string operationId,
        string? pathParamsJson = null,
        string? queryParamsJson = null,
        string? headersJson = null,
        string? bodyJson = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required", nameof(operationId));

        var doc = _store.RequireDocument();

        if (!TryFindOperation(doc, operationId, out var method, out var pathTemplate))
            throw new InvalidOperationException($"OperationId not found: {operationId}");

        var baseUrl = ResolveBaseUrl(doc, _runtime.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No base URL available. Call api_set_base_url or ensure the spec contains servers[].");

        var pathParams = ParseObject(pathParamsJson);
        var queryParams = ParseObject(queryParamsJson);
        var headers = ParseObject(headersJson);

        var path = ApplyPathParams(pathTemplate, pathParams);
        var url = BuildUrl(baseUrl, path, queryParams);

        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var kvp in headers)
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);

        if (!request.Headers.Contains("Authorization") && !string.IsNullOrWhiteSpace(_runtime.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _runtime.BearerToken);

        if (!string.IsNullOrWhiteSpace(bodyJson) &&
            (method is "POST" or "PUT" or "PATCH"))
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        var client = _httpClientFactory.CreateClient();

        var sw = Stopwatch.StartNew();
        using var response = await client.SendAsync(request);
        sw.Stop();

        var responseBody = await response.Content.ReadAsStringAsync();

        const int maxBodyChars = 50_000;
        if (responseBody.Length > maxBodyChars)
            responseBody = responseBody[..maxBodyChars] + "\n... (truncated)";

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

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
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
