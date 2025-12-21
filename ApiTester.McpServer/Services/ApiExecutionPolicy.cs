namespace ApiTester.McpServer.Services;

public sealed class ApiExecutionPolicy
{
    public bool DryRun { get; set; } = false;

    // Allowlist of base URLs you permit this server to call.
    public List<string> AllowedBaseUrls { get; set; } = new();

    // SSRF guardrails
    public bool BlockLocalhost { get; set; } = true;
    public bool BlockPrivateNetworks { get; set; } = true;

    // Start strict, expand later
    public HashSet<string> AllowedMethods { get; set; } =
        new(StringComparer.OrdinalIgnoreCase) { "GET" };

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;   // 256 KB
    public int MaxResponseBodyBytes { get; set; } = 512 * 1024;  // 512 KB
}
