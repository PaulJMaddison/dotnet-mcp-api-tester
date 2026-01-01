namespace ApiTester.AI;

public sealed class NullAiClient : IAiClient
{
    public Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct = default)
        => Task.FromResult(new AiResponse("AI client is not configured."));
}
