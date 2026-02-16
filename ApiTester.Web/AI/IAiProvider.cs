namespace ApiTester.Web.AI;

public interface IAiProvider
{
    Task<AiResult> ExplainApiAsync(string spec, string operationId, CancellationToken ct);
    Task<AiResult> SuggestEdgeCasesAsync(string spec, string operationId, CancellationToken ct);
    Task<AiResult> SummariseRunAsync(string runId, string runContext, CancellationToken ct);
    Task<AiResult> SuggestFixesAsync(string runId, string runContext, CancellationToken ct);
}
