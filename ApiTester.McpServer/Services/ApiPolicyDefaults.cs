namespace ApiTester.McpServer.Services;

public static class ApiPolicyDefaults
{
    public static ApiExecutionPolicy CreateSafeDefaults() => new()
    {
        DryRun = true,
        AllowedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" },
        AllowedBaseUrls = new List<string>(),
        BlockLocalhost = true,
        BlockPrivateNetworks = true,
        Timeout = TimeSpan.FromSeconds(10),
        MaxRequestBodyBytes = 256 * 1024,
        MaxResponseBodyBytes = 512 * 1024
    };

    public static void ApplySafeDefaults(ApiExecutionPolicy target)
    {
        var defaults = CreateSafeDefaults();
        target.DryRun = defaults.DryRun;
        target.BlockLocalhost = defaults.BlockLocalhost;
        target.BlockPrivateNetworks = defaults.BlockPrivateNetworks;
        target.Timeout = defaults.Timeout;
        target.MaxRequestBodyBytes = defaults.MaxRequestBodyBytes;
        target.MaxResponseBodyBytes = defaults.MaxResponseBodyBytes;

        target.AllowedMethods.Clear();
        foreach (var method in defaults.AllowedMethods) target.AllowedMethods.Add(method);

        target.AllowedBaseUrls.Clear();
        foreach (var url in defaults.AllowedBaseUrls) target.AllowedBaseUrls.Add(url);
    }
}
