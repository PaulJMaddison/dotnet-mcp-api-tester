namespace ApiTester.Rag.Answering;

public interface IChatCompletionClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}
