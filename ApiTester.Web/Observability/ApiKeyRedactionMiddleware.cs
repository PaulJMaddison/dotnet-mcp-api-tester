using ApiTester.Web.Auth;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Observability;

public sealed class ApiKeyRedactionMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyRedactionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.HeaderName, out var apiKeyHeader))
        {
            var rawApiKey = apiKeyHeader.ToString();
            if (!string.IsNullOrWhiteSpace(rawApiKey))
            {
                context.Items[ApiKeyAuthDefaults.RawApiKeyItemName] = rawApiKey;
                context.Request.Headers[ApiKeyAuthDefaults.HeaderName] = ApiKeyAuthDefaults.RedactedValue;
            }
        }

        if (context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.AuthorizationHeaderName, out var authorization))
        {
            var rawAuth = authorization.ToString();
            if (!string.IsNullOrWhiteSpace(rawAuth))
            {
                context.Items[ApiKeyAuthDefaults.RawAuthorizationItemName] = rawAuth;
                context.Request.Headers[ApiKeyAuthDefaults.AuthorizationHeaderName] = ApiKeyAuthDefaults.RedactedValue;
            }
        }

        await _next(context);
    }
}
