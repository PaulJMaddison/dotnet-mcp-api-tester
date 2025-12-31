using Microsoft.AspNetCore.Http;

namespace ApiTester.Ui.Auth;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyAuthSettings _settings;

    public ApiKeyAuthMiddleware(RequestDelegate next, ApiKeyAuthSettings settings)
    {
        _next = next;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.HeaderName, out var apiKeyHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var apiKey = apiKeyHeader.ToString();
        if (!_settings.IsAllowed(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Items[ApiKeyAuthDefaults.OwnerKeyItemName] = apiKey.Trim();
        await _next(context);
    }
}
