using ApiTester.Web.Auth;
using ApiTester.Web.Errors;

namespace ApiTester.Web.AbuseProtection;

public sealed class TenantIpRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public TenantIpRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantIpRateLimiter limiter)
    {
        var tenantId = context.GetTenantContext().TenantId;
        var category = EndpointCategoryResolver.Resolve(context.Request.Path);
        var ipAddress = ResolveIpAddress(context);

        if (!limiter.TryConsume(tenantId, ipAddress, category, out var retryAfter))
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            await ApiProblemFactory.Result(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "RateLimitExceeded",
                    "Rate limit exceeded",
                    $"Too many requests for tenant '{tenantId}' from IP '{ipAddress}' in '{category}' endpoints. Retry after {retryAfterSeconds} seconds.")
                .ExecuteAsync(context);
            return;
        }

        await _next(context);
    }

    private static string ResolveIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                ?? "unknown";
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
