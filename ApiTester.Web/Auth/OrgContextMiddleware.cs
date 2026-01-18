using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiTester.Web.Auth;

public sealed class OrgContextMiddleware
{
    public const string ContextItemName = "OrgContext";

    private readonly RequestDelegate _next;
    private readonly ILogger<OrgContextMiddleware> _logger;

    public OrgContextMiddleware(RequestDelegate next, ILogger<OrgContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, OrgContextResolver resolver)
    {
        var orgContext = await resolver.ResolveAsync(context, context.RequestAborted);
        if (orgContext is null)
        {
            _logger.LogWarning("Org context could not be resolved for request {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Items[ContextItemName] = orgContext;
        await _next(context);
    }
}
