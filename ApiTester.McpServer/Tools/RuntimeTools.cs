using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RuntimeTools
{
    private readonly ApiRuntimeConfig _runtime;

    public RuntimeTools(ApiRuntimeConfig runtime)
    {
        _runtime = runtime;
    }

    [McpServerTool, Description("Set the base URL used for executing API requests. Overrides servers[] in the OpenAPI spec.")]
    public object ApiSetBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("baseUrl is required", nameof(baseUrl));

        var trimmed = baseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new { isError = true, error = "Base URL must be an absolute http/https URL." };
        }

        _runtime.SetBaseUrl(trimmed);
        return new { ok = true, baseUrl = _runtime.BaseUrl };
    }

    [McpServerTool, Description("Set a Bearer token for the Authorization header used when executing API requests.")]
    public string ApiSetBearerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("token is required", nameof(token));

        _runtime.SetBearerToken(token);
        return "Bearer token set.";
    }

    [McpServerTool, Description("Clear any configured authentication used for executing API requests.")]
    public string ApiClearAuth()
    {
        _runtime.ClearAuth();
        return "Auth cleared.";
    }
}
