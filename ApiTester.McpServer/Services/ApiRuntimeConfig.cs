namespace ApiTester.McpServer.Services;

public sealed class ApiRuntimeConfig
{
    public string? BaseUrl { get; private set; }
    public string? BearerToken { get; private set; }

    public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl.Trim();
    public void SetBearerToken(string token) => BearerToken = token;
    public void ClearAuth() => BearerToken = null;

    public void ClearBaseUrl() => BaseUrl = null;

    public ApiExecutionPolicy Policy { get; } = new();

    public void ResetRuntime()
    {
        // Clear runtime state
        BaseUrl = null;
        BearerToken = null;

        // Reset policy from one shared source of truth
        ApiPolicyDefaults.ApplySafeDefaults(Policy);
    }
}
