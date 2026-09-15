using ApiTester.AI;
using ApiTester.Rag.Answering;
using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Rag;

public sealed class AiClientChatCompletionClient : IChatCompletionClient
{
    private readonly IAiClient _ai;
    private readonly QualificationTelemetry? _telemetry;

    public AiClientChatCompletionClient(IAiClient ai, QualificationTelemetry? telemetry = null)
    {
        _ai = ai;
        _telemetry = telemetry;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var resp = await _ai.GetResponseAsync(new AiPrompt(systemPrompt, userPrompt), ct).ConfigureAwait(false);
        _telemetry?.Emit("rag.reasoning.completed", new { deployment = resp.Model, durationMs = resp.ElapsedMs, inputTokens = resp.Usage.InputTokens, outputTokens = resp.Usage.OutputTokens, success = true });
        return resp.Content ?? string.Empty;
    }
}
