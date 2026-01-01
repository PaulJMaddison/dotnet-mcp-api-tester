namespace ApiTester.AI;

public sealed class MockAiClient : IAiClient
{
    private readonly Func<AiPrompt, AiResponse> _handler;

    public MockAiClient(Func<AiPrompt, AiResponse>? handler = null)
    {
        _handler = handler ?? (_ => new AiResponse("Mock AI response"));
    }

    public List<AiPrompt> ReceivedPrompts { get; } = new();

    public Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct = default)
    {
        ReceivedPrompts.Add(prompt);
        return Task.FromResult(_handler(prompt));
    }
}
