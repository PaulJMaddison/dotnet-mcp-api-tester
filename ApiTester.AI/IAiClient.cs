namespace ApiTester.AI;

public interface IAiClient
{
    Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct = default);
}
