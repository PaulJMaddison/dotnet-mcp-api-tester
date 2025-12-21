namespace ApiTester.McpServer.Services;

public static class ApiPolicyDefaults
{
    // Always return a NEW instance, never share a mutable singleton.
    public static ApiExecutionPolicy CreateSafeDefaults()
    {
        return new ApiExecutionPolicy
        {
            DryRun = true,
            AllowedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" },
            AllowedBaseUrls = new List<string>(),
            BlockLocalhost = true,
            BlockPrivateNetworks = true,
            Timeout = TimeSpan.FromSeconds(10),
            MaxRequestBodyBytes = 262_144,
            MaxResponseBodyBytes = 524_288
        };
    }

    // Apply defaults onto an existing policy instance (your runtime config holds this instance).
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
        foreach (var m in defaults.AllowedMethods)
            target.AllowedMethods.Add(m);

        target.AllowedBaseUrls.Clear();
        foreach (var u in defaults.AllowedBaseUrls)
            target.AllowedBaseUrls.Add(u);
    }
}
