namespace ApiTester.McpServer.Services;

public sealed class ApiExecutionPolicy
{
    public bool DryRun { get; set; } = true;
    public List<string> AllowedBaseUrls { get; set; } = new();
    public HashSet<string> AllowedMethods { get; set; } = new(StringComparer.OrdinalIgnoreCase) { "GET" };
    public bool BlockLocalhost { get; set; } = true;
    public bool BlockPrivateNetworks { get; set; } = true;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;
    public int MaxResponseBodyBytes { get; set; } = 512 * 1024;
}
