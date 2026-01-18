using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IAiInsightStore
{
    Task<IReadOnlyList<AiInsightRecord>> ListAsync(Guid organisationId, Guid projectId, Guid runId, CancellationToken ct);
    Task<IReadOnlyList<AiInsightRecord>> CreateAsync(
        Guid organisationId,
        Guid projectId,
        Guid runId,
        string operationId,
        IReadOnlyList<AiInsightCreate> insights,
        string modelId,
        DateTime createdUtc,
        CancellationToken ct);
}
