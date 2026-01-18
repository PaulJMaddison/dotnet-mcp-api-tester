using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestPlanDraftStore
{
    Task<TestPlanDraftRecord?> GetAsync(Guid draftId, CancellationToken ct);
    Task<TestPlanDraftRecord> CreateAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct);
}
