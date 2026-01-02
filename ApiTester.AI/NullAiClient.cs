namespace ApiTester.AI;

public sealed class NullAiClient : IAiClient
{
    public Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new AiResponse(
            Content: "AI client not configured.",
            Usage: new AiUsage(0, 0),
            ElapsedMs: 0,
            Model: "null",
            Cost: new AiCostEstimate(0m, 0m, 0m)));

    }
}
