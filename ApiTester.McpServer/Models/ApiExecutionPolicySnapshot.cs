using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Models;

public sealed record ApiExecutionPolicySnapshot(
    bool DryRun,
    IReadOnlyList<string> AllowedBaseUrls,
    IReadOnlyList<string> AllowedMethods,
    bool BlockLocalhost,
    bool BlockPrivateNetworks,
    int TimeoutSeconds,
    int MaxRequestBodyBytes,
    int MaxResponseBodyBytes,
    bool ValidateSchema,
    bool RetryOnFlake,
    int MaxRetries)
{
    public static ApiExecutionPolicySnapshot FromPolicy(ApiExecutionPolicy policy)
    {
        if (policy is null) throw new ArgumentNullException(nameof(policy));

        var allowedBaseUrls = policy.AllowedBaseUrls
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allowedMethods = policy.AllowedMethods
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ApiExecutionPolicySnapshot(
            policy.DryRun,
            allowedBaseUrls,
            allowedMethods,
            policy.BlockLocalhost,
            policy.BlockPrivateNetworks,
            (int)policy.Timeout.TotalSeconds,
            policy.MaxRequestBodyBytes,
            policy.MaxResponseBodyBytes,
            policy.ValidateSchema,
            policy.RetryOnFlake,
            policy.MaxRetries);
    }
}
