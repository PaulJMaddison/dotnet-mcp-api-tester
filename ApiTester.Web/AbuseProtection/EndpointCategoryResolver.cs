namespace ApiTester.Web.AbuseProtection;

public static class EndpointCategoryResolver
{
    public static EndpointCategory Resolve(PathString path)
    {
        if (path.StartsWithSegments("/api/ai", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/v1/ai", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/ai", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointCategory.Ai;
        }

        var value = path.Value ?? string.Empty;
        if (value.Contains("/runs/execute/", StringComparison.OrdinalIgnoreCase))
            return EndpointCategory.RunExecution;

        return EndpointCategory.Default;
    }
}
