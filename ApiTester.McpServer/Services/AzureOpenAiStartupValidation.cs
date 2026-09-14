using ApiTester.AI.Azure;

namespace ApiTester.McpServer.Services;

public sealed record AzureOpenAiStartupValidationResult(bool IsValid, string? ErrorMessage)
{
    public static AzureOpenAiStartupValidationResult Valid { get; } = new(true, null);
}

/// <summary>
/// Validates the Azure OpenAI process-start configuration before the MCP stdio
/// transport starts. Credentials are deliberately supplied out-of-band through
/// Azure Identity or process environment, never through an MCP tool.
/// </summary>
public static class AzureOpenAiStartupValidation
{
    public static AzureOpenAiStartupValidationResult Validate(AzureOpenAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            missing.Add("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(options.ChatDeployment))
            missing.Add("AZURE_OPENAI_CHAT_DEPLOYMENT");
        if (string.IsNullOrWhiteSpace(options.EmbeddingDeployment))
            missing.Add("AZURE_OPENAI_EMBEDDING_DEPLOYMENT");

        AzureOpenAiAuthenticationMode mode;
        try
        {
            mode = options.GetAuthenticationMode();
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, missing);
        }

        switch (mode)
        {
            case AzureOpenAiAuthenticationMode.None:
                missing.Add("AZURE_OPENAI_AUTHENTICATION");
                break;

            case AzureOpenAiAuthenticationMode.DefaultAzureCredential:
                try
                {
                    _ = options.GetCredentialSource();
                }
                catch (InvalidOperationException ex)
                {
                    return Invalid(ex.Message, missing);
                }
                break;

            case AzureOpenAiAuthenticationMode.ApiKey when string.IsNullOrWhiteSpace(options.ApiKey):
                missing.Add("AZURE_OPENAI_API_KEY");
                break;

            case AzureOpenAiAuthenticationMode.BearerToken when string.IsNullOrWhiteSpace(options.BearerToken):
                missing.Add("AZURE_OPENAI_AUTH_TOKEN");
                break;
        }

        if (missing.Count > 0)
            return Invalid("Azure OpenAI configuration is incomplete.", missing);

        try
        {
            options.ValidateChat();
            options.ValidateEmbedding();
            return AzureOpenAiStartupValidationResult.Valid;
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, Array.Empty<string>());
        }
    }

    private static AzureOpenAiStartupValidationResult Invalid(string reason, IEnumerable<string> missing)
    {
        var missingItems = missing.Distinct(StringComparer.Ordinal).ToArray();
        var lines = new List<string>
        {
            "API Tester MCP could not start.",
            string.Empty,
            reason
        };

        if (missingItems.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Missing:");
            lines.AddRange(missingItems.Select(item => $"  {item}"));
        }

        lines.AddRange(new[]
        {
            string.Empty,
            "Recommended local authentication (Microsoft Entra ID via Azure CLI):",
            "  az login",
            "  AZURE_OPENAI_AUTHENTICATION=DefaultAzureCredential",
            "  AZURE_OPENAI_CREDENTIAL_SOURCE=AzureCli",
            string.Empty,
            "Alternative Azure OpenAI API-key authentication:",
            "  AZURE_OPENAI_AUTHENTICATION=ApiKey",
            "  AZURE_OPENAI_API_KEY=<your key>",
            string.Empty,
            "Required in both modes:",
            "  AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com/openai/v1/",
            "  AZURE_OPENAI_CHAT_DEPLOYMENT=<chat deployment>",
            "  AZURE_OPENAI_EMBEDDING_DEPLOYMENT=<embedding deployment>",
            string.Empty,
            "Credentials are read by the local MCP process at startup and are never accepted through an MCP tool."
        });

        return new AzureOpenAiStartupValidationResult(false, string.Join(Environment.NewLine, lines));
    }
}
