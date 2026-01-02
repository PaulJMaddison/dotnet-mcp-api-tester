using ApiTester.AI;
using ApiTester.Rag.Answering;

namespace ApiTester.McpServer.Rag;

public sealed class AiClientChatCompletionClient : IChatCompletionClient
{
    private readonly IAiClient _ai;

    public AiClientChatCompletionClient(IAiClient ai)
    {
        _ai = ai;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var resp = await _ai.GetResponseAsync(new AiPrompt(systemPrompt, userPrompt), ct).ConfigureAwait(false);
        return resp.Content ?? string.Empty;
    }
}
