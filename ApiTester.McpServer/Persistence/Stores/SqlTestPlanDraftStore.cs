using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlTestPlanDraftStore : ITestPlanDraftStore
{
    private readonly ApiTesterDbContext _db;

    public SqlTestPlanDraftStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<TestPlanDraftRecord?> GetAsync(Guid draftId, CancellationToken ct)
    {
        var entity = await _db.TestPlanDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DraftId == draftId, ct);

        return entity is null
            ? null
            : new TestPlanDraftRecord(entity.DraftId, entity.ProjectId, entity.OperationId, entity.PlanJson, entity.CreatedUtc);
    }

    public async Task<TestPlanDraftRecord> CreateAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var entity = new TestPlanDraftEntity
        {
            DraftId = Guid.NewGuid(),
            ProjectId = projectId,
            OperationId = operationId.Trim(),
            PlanJson = planJson,
            CreatedUtc = createdUtc
        };

        _db.TestPlanDrafts.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new TestPlanDraftRecord(entity.DraftId, entity.ProjectId, entity.OperationId, entity.PlanJson, entity.CreatedUtc);
    }
}
