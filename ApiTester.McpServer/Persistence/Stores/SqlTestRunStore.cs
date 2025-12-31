using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlTestRunStore : ITestRunStore
{
    private readonly ApiTesterDbContext _db;

    public SqlTestRunStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(TestRunRecord record)
    {
        Console.Error.WriteLine("[server] Saving to sql test  store...");
        if (record is null) throw new ArgumentNullException(nameof(record));

        var projectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? "default" : record.ProjectKey.Trim();

        var project = await EnsureProjectAsync(projectKey);

        var run = new TestRunEntity
        {
            RunId = record.RunId,
            ProjectId = project.ProjectId,
            OperationId = record.OperationId,
            StartedUtc = record.StartedUtc.UtcDateTime,
            CompletedUtc = record.CompletedUtc.UtcDateTime,

            TotalCases = record.Result.TotalCases,
            Passed = record.Result.Passed,
            Failed = record.Result.Failed,
            Blocked = record.Result.Blocked,
            TotalDurationMs = record.Result.TotalDurationMs,

            Results = record.Result.Results.Select(r => new TestCaseResultEntity
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
        await _db.SaveChangesAsync();
    }

    public async Task<TestRunRecord?> GetAsync(Guid runId)
    {
        var run = await _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Results)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.RunId == runId);

        if (run is null) return null;

        return new TestRunRecord
        {
            RunId = run.RunId,
            ProjectKey = run.Project?.ProjectKey ?? "default",
            OperationId = run.OperationId,
            StartedUtc = new DateTimeOffset(run.StartedUtc, TimeSpan.Zero),
            CompletedUtc = new DateTimeOffset(run.CompletedUtc, TimeSpan.Zero),
            Result = new TestRunResult
            {
                OperationId = run.OperationId,
                TotalCases = run.TotalCases,
                Passed = run.Passed,
                Failed = run.Failed,
                Blocked = run.Blocked,
                TotalDurationMs = run.TotalDurationMs,
                Results = run.Results
                    .OrderBy(r => r.TestCaseResultId)
                    .Select(r => new TestCaseResult
                    {
                        Name = r.Name,
                        Blocked = r.Blocked,
                        BlockReason = r.BlockReason,
                        Method = r.Method,
                        Url = r.Url,
                        StatusCode = r.StatusCode,
                        DurationMs = r.DurationMs,
                        Pass = r.Pass,
                        FailureReason = r.FailureReason,
                        ResponseSnippet = r.ResponseSnippet
                    })
                    .ToList()
            }
        };
    }

    public async Task<PagedResult<TestRunRecord>> ListAsync(
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        var q = _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.Project.ProjectKey == projectKey);

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            var op = operationId.Trim();
            q = q.Where(x => x.OperationId == op);
        }

        var total = await q.CountAsync(CancellationToken.None);

        var ordered = sortField switch
        {
            SortField.CreatedUtc => direction == SortDirection.Asc
                ? q.OrderBy(x => x.StartedUtc)
                : q.OrderByDescending(x => x.StartedUtc),
            _ => direction == SortDirection.Asc
                ? q.OrderBy(x => x.StartedUtc)
                : q.OrderByDescending(x => x.StartedUtc)
        };

        var runs = await ordered
            .Skip(request.Offset)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        var items = runs.Select(run => new TestRunRecord
        {
            RunId = run.RunId,
            ProjectKey = run.Project?.ProjectKey ?? projectKey,
            OperationId = run.OperationId,
            StartedUtc = new DateTimeOffset(run.StartedUtc, TimeSpan.Zero),
            CompletedUtc = new DateTimeOffset(run.CompletedUtc, TimeSpan.Zero),
            Result = new TestRunResult
            {
                OperationId = run.OperationId,
                TotalCases = run.TotalCases,
                Passed = run.Passed,
                Failed = run.Failed,
                Blocked = run.Blocked,
                TotalDurationMs = run.TotalDurationMs,
                Results = new List<TestCaseResult>() // summaries don’t need case results
            }
        }).ToList();

        var itemCount = items.Count;
        int? nextOffset = request.Offset + itemCount < total
            ? request.Offset + itemCount
            : null;

        return new PagedResult<TestRunRecord>(items, total, nextOffset);
    }

    private async Task<ProjectEntity> EnsureProjectAsync(string projectKey)
    {
        var existing = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectKey == projectKey);
        if (existing is not null) return existing;

        var created = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            ProjectKey = projectKey,
            Name = projectKey,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(created);
        await _db.SaveChangesAsync();
        return created;
    }
}
