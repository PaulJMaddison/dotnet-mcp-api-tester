namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ChatDeployment { get; init; } = string.Empty;
    public string EmbeddingDeployment { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string BearerToken { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 2;
    public int MaxResponseBytes { get; init; } = 1_048_576;
    public int MaxInputChars { get; init; } = 120_000;
    public int MaxCompletionTokens { get; init; } = 1_500;
    public int CircuitBreakerFailureThreshold { get; init; } = 4;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ApiKey) || !string.IsNullOrWhiteSpace(BearerToken);

    public bool IsChatConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ChatDeployment);

    public bool IsEmbeddingConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(EmbeddingDeployment);

    public Uri GetApiBaseUri()
    {
        if (!Uri.TryCreate(Endpoint?.Trim(), UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("AzureOpenAI:Endpoint must be an absolute URI.");

        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Azure OpenAI endpoint must use HTTPS.");

        var value = endpoint.ToString().TrimEnd('/');
        if (!value.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            value += "/openai/v1";

        return new Uri(value + "/", UriKind.Absolute);
    }
}
