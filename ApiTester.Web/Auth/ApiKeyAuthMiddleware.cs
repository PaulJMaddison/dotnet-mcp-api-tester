using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Errors;
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
            await ApiProblemFactory.Result(context, StatusCodes.Status401Unauthorized, "ApiTokenMissing", "API token missing", "Provide an API key using Authorization Bearer or X-Api-Key.").ExecuteAsync(context);
            return;
        }

        if (!ApiKeyToken.TryGetPrefix(apiKey, out var prefix))
        {
            await ApiProblemFactory.Result(context, StatusCodes.Status401Unauthorized, "ApiTokenInvalid", "Invalid API token", "The provided API token format is invalid.").ExecuteAsync(context);
            return;
        }

        var candidates = await apiKeys.ListByPrefixAsync(prefix, context.RequestAborted);
        var record = candidates.FirstOrDefault(candidate => ApiKeyHasher.Verify(apiKey, candidate.Hash));
        if (record is null)
        {
            await ApiProblemFactory.Result(context, StatusCodes.Status401Unauthorized, "ApiTokenInvalid", "Invalid API token", "No active API token matched the supplied credentials.").ExecuteAsync(context);
            return;
        }

        if (!ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow))
        {
            await ApiProblemFactory.Result(context, StatusCodes.Status401Unauthorized, "ApiTokenRevoked", "API token not active", "The provided API token is expired or revoked.").ExecuteAsync(context);
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
