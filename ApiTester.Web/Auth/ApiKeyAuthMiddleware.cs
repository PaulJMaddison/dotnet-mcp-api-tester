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
        var apiKey = ResolveToken(context);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!ApiKeyToken.TryGetPrefix(apiKey, out var prefix))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var candidates = await apiKeys.ListByPrefixAsync(prefix, context.RequestAborted);
        var record = candidates.FirstOrDefault(candidate => ApiKeyHasher.Verify(apiKey, candidate.Hash));
        if (record is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await apiKeys.TouchLastUsedAsync(record.KeyId, DateTime.UtcNow, context.RequestAborted);

        var scopes = ApiKeyScopes.Parse(record.Scopes);
        var apiKeyContext = new ApiKeyContext(record.KeyId, record.OrganisationId, record.UserId, scopes, record.ExpiresUtc, record.RevokedUtc, record.Prefix);
        context.Items[ApiKeyAuthDefaults.ApiKeyContextItemName] = apiKeyContext;
        await _next(context);
    }

    private static string? ResolveToken(HttpContext context)
    {
        if (context.Items.TryGetValue(ApiKeyAuthDefaults.RawAuthorizationItemName, out var rawAuthorization) && rawAuthorization is string rawAuth)
        {
            var authValue = rawAuth;
            if (authValue.StartsWith(ApiKeyAuthDefaults.BearerScheme + " ", StringComparison.OrdinalIgnoreCase))
            {
                var bearerToken = authValue[(ApiKeyAuthDefaults.BearerScheme.Length + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(bearerToken))
                    return bearerToken;
            }
        }
        else if (context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.AuthorizationHeaderName, out var authorization))
        {
            var authValue = authorization.ToString();
            if (authValue.StartsWith(ApiKeyAuthDefaults.BearerScheme + " ", StringComparison.OrdinalIgnoreCase))
            {
                var bearerToken = authValue[(ApiKeyAuthDefaults.BearerScheme.Length + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(bearerToken))
                    return bearerToken;
            }
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.HeaderName, out var apiKeyHeader))
            return null;

        return context.Items.TryGetValue(ApiKeyAuthDefaults.RawApiKeyItemName, out var rawKey) && rawKey is string rawValue
            ? rawValue
            : apiKeyHeader.ToString();
    }
}
