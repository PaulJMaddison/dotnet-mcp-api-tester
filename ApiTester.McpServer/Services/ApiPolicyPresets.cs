namespace ApiTester.McpServer.Services;

public static class ApiPolicyPresets
{
    public static ApiExecutionPolicy SafeDefaults() => new()
    {
        DryRun = true,
        AllowedBaseUrls = new(),
        AllowedMethods = new() { "GET" },
        BlockLocalhost = true,
        BlockPrivateNetworks = true,
        Timeout = TimeSpan.FromSeconds(10),
        MaxRequestBodyBytes = 262_144,
        MaxResponseBodyBytes = 524_288,
        RetryOnFlake = false,
        MaxRetries = 0
    };

    public static ApiExecutionPolicy AllowPetstore() => new()
    {
        DryRun = false,
        AllowedBaseUrls = new() { "https://petstore3.swagger.io/api/v3" },
        AllowedMethods = new() { "GET" },
        BlockLocalhost = true,
        BlockPrivateNetworks = true,
        Timeout = TimeSpan.FromSeconds(10),
        MaxRequestBodyBytes = 262_144,
        MaxResponseBodyBytes = 524_288,
        RetryOnFlake = false,
        MaxRetries = 0
    };
}
