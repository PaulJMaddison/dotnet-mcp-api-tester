using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.Execution;

public static class EnvironmentSelector
{
    public static bool TryApplyBaseUrl(
        ApiRuntimeConfig runtime,
        OpenApiDocument doc,
        string? environmentBaseUrl,
        out string? baseUrl,
        out string error)
    {
        if (!string.IsNullOrWhiteSpace(environmentBaseUrl))
        {
            baseUrl = NormalizeBaseUrl(environmentBaseUrl);
            runtime.SetBaseUrl(baseUrl);
            error = string.Empty;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(runtime.BaseUrl))
        {
            baseUrl = NormalizeBaseUrl(runtime.BaseUrl);
            runtime.SetBaseUrl(baseUrl);
            error = string.Empty;
            return true;
        }

        var resolved = ResolveBaseUrl(doc);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            baseUrl = null;
            error = "OpenAPI spec does not define servers and no runtime base URL is configured.";
            return false;
        }

        baseUrl = NormalizeBaseUrl(resolved);
        runtime.SetBaseUrl(baseUrl);
        error = string.Empty;
        return true;
    }

    internal static string? ResolveBaseUrl(OpenApiDocument doc)
    {
        if (doc.Servers is null || doc.Servers.Count == 0)
            return null;

        var serverUrl = doc.Servers[0].Url;
        return string.IsNullOrWhiteSpace(serverUrl) ? null : serverUrl.Trim().TrimEnd('/');
    }

    private static string NormalizeBaseUrl(string baseUrl)
        => baseUrl.Trim().TrimEnd('/');
}
