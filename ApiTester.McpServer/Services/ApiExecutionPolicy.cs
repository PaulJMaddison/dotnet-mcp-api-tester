namespace ApiTester.McpServer.Services;

public sealed class ApiExecutionPolicy
{
    // SAFE SaaS defaults
    public bool DryRun { get; set; } = true;

    // Deny-by-default: nothing allowed unless caller sets it
    public List<string> AllowedBaseUrls { get; set; } = new();

    // Start strict, expand later
    public HashSet<string> AllowedMethods { get; set; } =
        new(StringComparer.OrdinalIgnoreCase) { "GET" };

    // SSRF guardrails
    public bool BlockLocalhost { get; set; } = true;
    public bool BlockPrivateNetworks { get; set; } = true;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;   // 256 KB
    public int MaxResponseBodyBytes { get; set; } = 512 * 1024;  // 512 KB

    public bool RetryOnFlake { get; set; }
    public int MaxRetries { get; set; }
}
