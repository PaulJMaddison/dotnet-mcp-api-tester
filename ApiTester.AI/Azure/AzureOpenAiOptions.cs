namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AI:AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ChatDeployment { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Authentication { get; set; } = "DefaultAzureCredential";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 2048;

    public bool IsConfigured =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(ChatDeployment) &&
        string.Equals(Authentication, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase);
}
