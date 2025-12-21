namespace ApiTester.McpServer.Services;

public sealed class ApiRuntimeConfig
{
    public ApiRuntimeConfig()
    {
        Policy.AllowedBaseUrls.Add("https://petstore3.swagger.io/api/v3");
        Policy.AllowedBaseUrls.Add("http://localhost"); // for SmokeApi (any port)
        Policy.AllowedBaseUrls.Add("https://localhost"); // optional
    }

    public string? BaseUrl { get; private set; }
    public string? BearerToken { get; private set; }

    public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl.Trim();
    public void SetBearerToken(string token) => BearerToken = token;
    public void ClearAuth() => BearerToken = null;
    public ApiExecutionPolicy Policy { get; } = new();

}
