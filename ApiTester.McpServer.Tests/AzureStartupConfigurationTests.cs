using ApiTester.AI.Azure;
using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Tests;

public sealed class AzureStartupConfigurationTests
{
    [Fact]
    public void AzureCliAuthenticationIsValidWithoutApiKey()
    {
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions
        {
            Endpoint = "https://example.openai.azure.com/openai/v1/",
            ChatDeployment = "chat",
            EmbeddingDeployment = "embedding",
            Authentication = AzureOpenAiOptions.DefaultAzureCredentialAuthentication,
            CredentialSource = AzureOpenAiOptions.AzureCliCredentialSource
        });

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ApiKeyAuthenticationIsValidWhenKeyIsSupplied()
    {
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions
        {
            Endpoint = "https://example.openai.azure.com/openai/v1/",
            ChatDeployment = "chat",
            EmbeddingDeployment = "embedding",
            Authentication = AzureOpenAiOptions.ApiKeyAuthentication,
            ApiKey = "super-secret-unit-test-key"
        });

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void MissingConfigurationProducesActionableMcpSafeGuidance()
    {
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions());

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("AZURE_OPENAI_ENDPOINT", result.ErrorMessage);
        Assert.Contains("AZURE_OPENAI_CHAT_DEPLOYMENT", result.ErrorMessage);
        Assert.Contains("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", result.ErrorMessage);
        Assert.Contains("AZURE_OPENAI_AUTHENTICATION", result.ErrorMessage);
        Assert.Contains("az login", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AZURE_OPENAI_API_KEY=<your key>", result.ErrorMessage);
        Assert.Contains("never accepted through an MCP tool", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApiKeyModeWithoutKeyNamesMissingEnvironmentVariable()
    {
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions
        {
            Endpoint = "https://example.openai.azure.com/openai/v1/",
            ChatDeployment = "chat",
            EmbeddingDeployment = "embedding",
            Authentication = AzureOpenAiOptions.ApiKeyAuthentication
        });

        Assert.False(result.IsValid);
        Assert.Contains("AZURE_OPENAI_API_KEY", result.ErrorMessage);
    }

    [Fact]
    public void ValidationMessageNeverEchoesConfiguredSecrets()
    {
        const string secret = "DO-NOT-PRINT-THIS-SECRET";
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions
        {
            Endpoint = "not-a-valid-uri",
            ChatDeployment = "chat",
            EmbeddingDeployment = "embedding",
            Authentication = AzureOpenAiOptions.ApiKeyAuthentication,
            ApiKey = secret
        });

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain(secret, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAzureCredentialSourceProducesGuidanceWithoutRequiringSecret()
    {
        var result = AzureOpenAiStartupValidation.Validate(new AzureOpenAiOptions
        {
            Endpoint = "https://example.openai.azure.com/openai/v1/",
            ChatDeployment = "chat",
            EmbeddingDeployment = "embedding",
            Authentication = AzureOpenAiOptions.DefaultAzureCredentialAuthentication,
            CredentialSource = "VisualStudio"
        });

        Assert.False(result.IsValid);
        Assert.Contains("Default, AzureCli, or ManagedIdentity", result.ErrorMessage);
        Assert.Contains("AZURE_OPENAI_CREDENTIAL_SOURCE=AzureCli", result.ErrorMessage);
    }
}
