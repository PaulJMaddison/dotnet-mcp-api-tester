namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiOptions
{
    public const string DefaultAzureCredentialAuthentication = "DefaultAzureCredential";
    public const string ApiKeyAuthentication = "ApiKey";
    public const string BearerTokenAuthentication = "BearerToken";

    public const string DefaultCredentialSource = "Default";
    public const string AzureCliCredentialSource = "AzureCli";
    public const string ManagedIdentityCredentialSource = "ManagedIdentity";

    public string Endpoint { get; init; } = string.Empty;
    public string ChatDeployment { get; init; } = string.Empty;
    public string EmbeddingDeployment { get; init; } = string.Empty;
    public string Authentication { get; init; } = string.Empty;
    public string CredentialSource { get; init; } = DefaultCredentialSource;
    public string ApiKey { get; init; } = string.Empty;
    public string BearerToken { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 2;
    public int MaxResponseBytes { get; init; } = 1_048_576;
    public int MaxInputChars { get; init; } = 120_000;

    public int MaxCompletionTokens { get; init; }
    public int CircuitBreakerFailureThreshold { get; init; } = 4;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;

    public bool HasCredentials => GetAuthenticationMode() switch
    {
        AzureOpenAiAuthenticationMode.DefaultAzureCredential => true,
        AzureOpenAiAuthenticationMode.ApiKey => !string.IsNullOrWhiteSpace(ApiKey),
        AzureOpenAiAuthenticationMode.BearerToken => !string.IsNullOrWhiteSpace(BearerToken),
        _ => false
    };

    public bool IsChatConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ChatDeployment);

    public bool IsEmbeddingConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(EmbeddingDeployment);

    public void ValidateCommon()
    {
        _ = GetApiBaseUri();
        var authenticationMode = GetAuthenticationMode();
        if (authenticationMode == AzureOpenAiAuthenticationMode.DefaultAzureCredential)
            _ = GetCredentialSource();

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("AzureOpenAI:TimeoutSeconds must be greater than zero.");
        if (MaxRetries < 0)
            throw new InvalidOperationException("AzureOpenAI:MaxRetries must not be negative.");
        if (MaxResponseBytes <= 0)
            throw new InvalidOperationException("AzureOpenAI:MaxResponseBytes must be greater than zero.");
        if (MaxInputChars <= 0)
            throw new InvalidOperationException("AzureOpenAI:MaxInputChars must be greater than zero.");
        if (CircuitBreakerFailureThreshold <= 0)
            throw new InvalidOperationException("AzureOpenAI:CircuitBreakerFailureThreshold must be greater than zero.");
        if (CircuitBreakerBreakSeconds <= 0)
            throw new InvalidOperationException("AzureOpenAI:CircuitBreakerBreakSeconds must be greater than zero.");
    }

    public void ValidateChat()
    {
        ValidateCommon();
        if (!IsChatConfigured)
            throw new InvalidOperationException(
                "Azure OpenAI chat is not fully configured. Endpoint, ChatDeployment and credentials are required.");
    }

    public void ValidateEmbedding()
    {
        ValidateCommon();
        if (!IsEmbeddingConfigured)
            throw new InvalidOperationException(
                "Azure OpenAI embeddings are not fully configured. Endpoint, EmbeddingDeployment and credentials are required.");
    }

    public Uri GetApiBaseUri()
    {
        if (!Uri.TryCreate(Endpoint?.Trim(), UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("AzureOpenAI:Endpoint must be an absolute URI.");

        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Azure OpenAI endpoint must use HTTPS.");

        if (!string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new InvalidOperationException("AzureOpenAI:Endpoint must not contain a query string or fragment.");

        var value = endpoint.ToString().TrimEnd('/');
        if (!value.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            value += "/openai/v1";

        return new Uri(value + "/", UriKind.Absolute);
    }

    public AzureOpenAiAuthenticationMode GetAuthenticationMode()
    {
        var configured = Authentication?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            if (configured.Equals(DefaultAzureCredentialAuthentication, StringComparison.OrdinalIgnoreCase))
                return AzureOpenAiAuthenticationMode.DefaultAzureCredential;
            if (configured.Equals(ApiKeyAuthentication, StringComparison.OrdinalIgnoreCase))
                return AzureOpenAiAuthenticationMode.ApiKey;
            if (configured.Equals(BearerTokenAuthentication, StringComparison.OrdinalIgnoreCase))
                return AzureOpenAiAuthenticationMode.BearerToken;

            throw new InvalidOperationException(
                "AzureOpenAI:Authentication must be DefaultAzureCredential, ApiKey, or BearerToken.");
        }

        if (!string.IsNullOrWhiteSpace(BearerToken))
            return AzureOpenAiAuthenticationMode.BearerToken;
        if (!string.IsNullOrWhiteSpace(ApiKey))
            return AzureOpenAiAuthenticationMode.ApiKey;

        return AzureOpenAiAuthenticationMode.None;
    }

    public AzureOpenAiCredentialSource GetCredentialSource()
    {
        var configured = string.IsNullOrWhiteSpace(CredentialSource)
            ? DefaultCredentialSource
            : CredentialSource.Trim();

        if (configured.Equals(DefaultCredentialSource, StringComparison.OrdinalIgnoreCase))
            return AzureOpenAiCredentialSource.Default;
        if (configured.Equals(AzureCliCredentialSource, StringComparison.OrdinalIgnoreCase))
            return AzureOpenAiCredentialSource.AzureCli;
        if (configured.Equals(ManagedIdentityCredentialSource, StringComparison.OrdinalIgnoreCase))
            return AzureOpenAiCredentialSource.ManagedIdentity;

        throw new InvalidOperationException(
            "AzureOpenAI:CredentialSource must be Default, AzureCli, or ManagedIdentity.");
    }
}

public enum AzureOpenAiAuthenticationMode
{
    None,
    DefaultAzureCredential,
    ApiKey,
    BearerToken
}

public enum AzureOpenAiCredentialSource
{
    Default,
    AzureCli,
    ManagedIdentity
}
