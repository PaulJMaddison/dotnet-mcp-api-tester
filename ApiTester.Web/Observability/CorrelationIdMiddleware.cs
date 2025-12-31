using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiTester.Web.Observability;

public static class CorrelationIdDefaults
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemName = "CorrelationId";
}

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[CorrelationIdDefaults.ItemName] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdDefaults.HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["correlationId"] = correlationId
               }))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdDefaults.HeaderName, out var header))
        {
            var value = header.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return Guid.NewGuid().ToString("N");
    }
}
