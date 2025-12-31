using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlTestPlanStore : ITestPlanStore
{
    private readonly ApiTesterDbContext _db;

    public SqlTestPlanStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<TestPlanRecord?> GetAsync(Guid projectId, string operationId, CancellationToken ct)
    {
        var op = operationId.Trim();
        var entity = await _db.TestPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.OperationId == op, ct);

        return entity is null
            ? null
            : new TestPlanRecord(entity.ProjectId, entity.OperationId, entity.PlanJson, entity.CreatedUtc);
    }

    public async Task<TestPlanRecord> UpsertAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct)
    {
        var op = operationId.Trim();
        var entity = await _db.TestPlans.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.OperationId == op, ct);
        if (entity is null)
        {
            entity = new TestPlanEntity
            {
                ProjectId = projectId,
                OperationId = op
            };
            _db.TestPlans.Add(entity);
        }

        entity.PlanJson = planJson;
        entity.CreatedUtc = createdUtc;

        await _db.SaveChangesAsync(ct);

        return new TestPlanRecord(entity.ProjectId, entity.OperationId, entity.PlanJson, entity.CreatedUtc);
    }
}
