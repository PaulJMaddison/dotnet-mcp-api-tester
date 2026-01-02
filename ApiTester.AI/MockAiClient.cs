using System.Collections.Concurrent;

namespace ApiTester.AI;

public sealed class MockAiClient : IAiClient
{
    private readonly Func<AiPrompt, AiResponse> _handler;
    private readonly ConcurrentQueue<AiPrompt> _received = new();

    public IReadOnlyCollection<AiPrompt> ReceivedPrompts => _received.ToArray();

    public MockAiClient(Func<AiPrompt, AiResponse>? handler = null)
    {
        _handler = handler ?? DefaultHandler;
    }

    public Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _received.Enqueue(prompt);
        return Task.FromResult(_handler(prompt));
    }

    private static AiResponse DefaultHandler(AiPrompt prompt)
        => new(
            Content: "mock",
            Usage: new AiUsage(0, 0),
            ElapsedMs: 0,
            Model: "mock",
            Cost: new AiCostEstimate(0m, 0m, 0m));
}
