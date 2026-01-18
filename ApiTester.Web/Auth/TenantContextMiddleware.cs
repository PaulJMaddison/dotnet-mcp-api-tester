using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiTester.Web.Auth;

public sealed class TenantContextMiddleware
{
    public const string ContextItemName = "TenantContext";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var apiKeyContext = context.GetApiKeyContext();
        if (apiKeyContext is null)
        {
            _logger.LogWarning("Tenant context could not be resolved for request {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items[ContextItemName] = new TenantContext(apiKeyContext.OrganisationId);
        await _next(context);
    }
}
