namespace ApiTester.Web.AI;

public sealed class OpenAiProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public string ProModel { get; set; } = "gpt-4o";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 2;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerBreakSeconds { get; set; } = 60;
    public int MaxInputChars { get; set; } = 24_000;
    public int MaxOutputChars { get; set; } = 8_000;
    public int MaxResponseBytes { get; set; } = 64_000;
}
