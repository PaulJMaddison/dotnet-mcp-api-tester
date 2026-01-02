namespace ApiTester.AI.Azure;

// This is intentionally a shell for now, the demo uses LocalGroundedAiClient.
// In production you would implement this using Managed Identity and Azure OpenAI.
public sealed class AzureOpenAiClient : IAiClient
{
    public Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct)
        => throw new InvalidOperationException("Azure OpenAI is not configured. Set AzureOpenAI settings.");
}
