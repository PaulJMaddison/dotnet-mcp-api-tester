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

    public Uri GetBaseUri()
    {
        if (!Uri.TryCreate(BaseUrl?.Trim(), UriKind.Absolute, out var uri))
            throw new InvalidOperationException("AI:OpenAI:BaseUrl must be an absolute URI.");
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI:OpenAI:BaseUrl must use HTTPS.");
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("AI:OpenAI:BaseUrl must not contain a query string or fragment.");

        return new Uri(uri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
    }

    public void Validate()
    {
        _ = GetBaseUri();

        if (string.IsNullOrWhiteSpace(DefaultModel))
            throw new InvalidOperationException("AI:OpenAI:DefaultModel is required.");
        if (string.IsNullOrWhiteSpace(ProModel))
            throw new InvalidOperationException("AI:OpenAI:ProModel is required.");
        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("AI:OpenAI:TimeoutSeconds must be greater than zero.");
        if (MaxRetries < 0)
            throw new InvalidOperationException("AI:OpenAI:MaxRetries must not be negative.");
        if (CircuitBreakerFailureThreshold <= 0)
            throw new InvalidOperationException("AI:OpenAI:CircuitBreakerFailureThreshold must be greater than zero.");
        if (CircuitBreakerBreakSeconds <= 0)
            throw new InvalidOperationException("AI:OpenAI:CircuitBreakerBreakSeconds must be greater than zero.");
        if (MaxInputChars <= 0)
            throw new InvalidOperationException("AI:OpenAI:MaxInputChars must be greater than zero.");
        if (MaxOutputChars <= 0)
            throw new InvalidOperationException("AI:OpenAI:MaxOutputChars must be greater than zero.");
        if (MaxResponseBytes <= 0)
            throw new InvalidOperationException("AI:OpenAI:MaxResponseBytes must be greater than zero.");
    }
}
