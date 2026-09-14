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

    [McpServerTool, Description("Set the base URL used for executing API requests. Overrides servers[] in the OpenAPI spec for this process only.")]
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
        return new { ok = true, baseUrl = _runtime.BaseUrl, persistence = "none" };
    }

    [McpServerTool, Description("Set a Bearer token in memory for API requests made by this MCP process.")]
    public string ApiSetBearerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("token is required", nameof(token));

        _runtime.SetBearerToken(token);
        return "Bearer token set in memory for this process.";
    }

    [McpServerTool, Description("Clear any configured API authentication from this process.")]
    public string ApiClearAuth()
    {
        _runtime.ClearAuth();
        return "Auth cleared.";
    }
}
