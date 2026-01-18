using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Auth;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyStore apiKeys)
    {
        if (!context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.HeaderName, out var apiKeyHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var apiKey = apiKeyHeader.ToString();
        if (!ApiKeyToken.TryGetPrefix(apiKey, out var prefix))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var record = await apiKeys.GetByPrefixAsync(prefix, context.RequestAborted);
        if (record is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!ApiKeyHasher.Verify(apiKey, record.Hash) || !ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var scopes = ApiKeyScopes.Parse(record.Scopes);
        var apiKeyContext = new ApiKeyContext(record.KeyId, record.OrganisationId, record.UserId, scopes, record.ExpiresUtc, record.RevokedUtc, record.Prefix);
        context.Items[ApiKeyAuthDefaults.ApiKeyContextItemName] = apiKeyContext;
        await _next(context);
    }
}
