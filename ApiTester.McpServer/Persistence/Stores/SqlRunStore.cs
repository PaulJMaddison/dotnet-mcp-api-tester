using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlRunStore : IRunStore
{
    private readonly ApiTesterDbContext _db;

    public SqlRunStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(Guid projectId, Guid runId, string operationId, DateTime startedUtc, DateTime completedUtc, TestRunResult result, CancellationToken ct)
    {
        var run = new TestRunEntity
        {
            RunId = runId,
            ProjectId = projectId,
            OperationId = operationId,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            TotalCases = result.TotalCases,
            Passed = result.Passed,
            Failed = result.Failed,
            Blocked = result.Blocked,
            TotalDurationMs = result.TotalDurationMs,
            Results = result.Results.Select(r => new TestCaseResultEntity
            {
                Name = r.Name ?? "",
                Blocked = r.Blocked,
                BlockReason = r.BlockReason,
                Method = r.Method ?? "",
                Url = r.Url,
                StatusCode = r.StatusCode,
                DurationMs = r.DurationMs,
                Pass = r.Pass,
                FailureReason = r.FailureReason,
                ResponseSnippet = r.ResponseSnippet
            }).ToList()
        };

        _db.TestRuns.Add(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<object?> GetAsync(Guid runId, CancellationToken ct)
    {
        var run = await _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.RunId == runId, ct);

        if (run is null) return null;

        return new
        {
            runId = run.RunId,
            projectId = run.ProjectId,
            operationId = run.OperationId,
            startedUtc = run.StartedUtc,
            completedUtc = run.CompletedUtc,
            result = new
            {
                operationId = run.OperationId,
                totalCases = run.TotalCases,
                passed = run.Passed,
                failed = run.Failed,
                blocked = run.Blocked,
                totalDurationMs = run.TotalDurationMs,
                results = run.Results
                    .OrderBy(x => x.TestCaseResultId)
                    .Select(r => new
                    {
                        name = r.Name,
                        blocked = r.Blocked,
                        blockReason = r.BlockReason,
                        method = r.Method,
                        url = r.Url,
                        statusCode = r.StatusCode,
                        durationMs = r.DurationMs,
                        pass = r.Pass,
                        failureReason = r.FailureReason,
                        responseSnippet = r.ResponseSnippet
                    })
            }
        };
    }

    public async Task<object> ListAsync(Guid? projectId, int take, CancellationToken ct)
    {
        take = take <= 0 ? 20 : Math.Min(take, 200);

        var q = _db.TestRuns.AsNoTracking();

        if (projectId.HasValue)
            q = q.Where(x => x.ProjectId == projectId.Value);

        var runs = await q
            .OrderByDescending(x => x.StartedUtc)
            .Take(take)
            .Select(x => new
            {
                runId = x.RunId,
                projectId = x.ProjectId,
                operationId = x.OperationId,
                startedUtc = x.StartedUtc,
                completedUtc = x.CompletedUtc,
                summary = new
                {
                    totalCases = x.TotalCases,
                    passed = x.Passed,
                    failed = x.Failed,
                    blocked = x.Blocked,
                    totalDurationMs = x.TotalDurationMs
                }
            })
            .ToListAsync(ct);

        return new
        {
            take,
            total = runs.Count,
            runs
        };
    }
}
