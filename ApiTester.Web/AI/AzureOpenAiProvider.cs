using ApiTester.AI;

namespace ApiTester.Web.AI;

public sealed class AzureOpenAiProvider(IAiClient client) : IAiProvider
{
    public Task<AiResult> ExplainApiAsync(string spec, string operationId, CancellationToken ct)
        => CompleteAsync($"Explain operation {operationId}", spec, ct);

    public Task<AiResult> SuggestEdgeCasesAsync(string spec, string operationId, CancellationToken ct)
        => CompleteAsync($"Suggest edge cases for operation {operationId}", spec, ct);

    public Task<AiResult> SummariseRunAsync(string runId, string runContext, CancellationToken ct)
        => CompleteAsync($"Summarise run {runId}", runContext, ct);

    public Task<AiResult> SuggestFixesAsync(string runId, string runContext, CancellationToken ct)
        => CompleteAsync($"Suggest improvements for run {runId}", runContext, ct);

    private async Task<AiResult> CompleteAsync(string task, string context, CancellationToken ct)
    {
        var prompt = new AiPrompt(
            "You are a strict API testing assistant. Return valid JSON only.",
            $"{task}. Return valid JSON only.\n\nContext:\n{context}");
        var response = await client.GetResponseAsync(prompt, ct);
        return new AiResult(response.Content, response.Model);
    }
}
