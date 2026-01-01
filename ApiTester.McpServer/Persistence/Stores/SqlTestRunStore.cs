using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlTestRunStore : ITestRunStore
{
    private readonly ApiTesterDbContext _db;
    private readonly ILogger<SqlTestRunStore> _logger;

    public SqlTestRunStore(ApiTesterDbContext db, ILogger<SqlTestRunStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveAsync(TestRunRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var projectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? "default" : record.ProjectKey.Trim();
        var ownerKey = string.IsNullOrWhiteSpace(record.OwnerKey) ? OwnerKeyDefaults.Default : record.OwnerKey.Trim();

        record.Result.ClassificationSummary = ResultClassificationRules.Summarize(record.Result.Results);

        var project = await EnsureProjectAsync(projectKey, ownerKey);
        _logger.LogInformation(
            "Saving run {RunId} for owner {OwnerKey} project {ProjectKey} (projectId {ProjectId})",
            record.RunId,
            ownerKey,
            projectKey,
            project.ProjectId);

        var run = new TestRunEntity
        {
            RunId = record.RunId,
            ProjectId = project.ProjectId,
            OperationId = record.OperationId,
            SpecId = record.SpecId,
            BaselineRunId = record.BaselineRunId,
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
                ResponseSnippet = r.ResponseSnippet,
                Classification = r.Classification
            }).ToList()
        };

        _db.TestRuns.Add(run);
        await _db.SaveChangesAsync();
    }

    public async Task<TestRunRecord?> GetAsync(string ownerKey, Guid runId)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();

        var run = await _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Results)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.RunId == runId && x.Project.OwnerKey == ownerKey);

        if (run is null) return null;

        var caseResults = run.Results
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
                ResponseSnippet = r.ResponseSnippet,
                Classification = r.Classification ?? ResultClassification.Pass
            })
            .ToList();

        var classificationSummary = ResultClassificationRules.Summarize(caseResults);

        return new TestRunRecord
        {
            RunId = run.RunId,
            OwnerKey = run.Project?.OwnerKey ?? OwnerKeyDefaults.Default,
            ProjectKey = run.Project?.ProjectKey ?? "default",
            OperationId = run.OperationId,
            SpecId = run.SpecId,
            BaselineRunId = run.BaselineRunId,
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
                ClassificationSummary = classificationSummary,
                Results = caseResults
            }
        };
    }

    public async Task<bool> SetBaselineAsync(string ownerKey, Guid runId, Guid baselineRunId)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();

        var run = await _db.TestRuns
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.RunId == runId && x.Project.OwnerKey == ownerKey);
        if (run is null)
            return false;

        var baselineExists = await _db.TestRuns
            .Include(x => x.Project)
            .AnyAsync(x => x.RunId == baselineRunId && x.Project.OwnerKey == ownerKey);
        if (!baselineExists)
            return false;

        run.BaselineRunId = baselineRunId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<TestRunRecord>> ListAsync(
        string ownerKey,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        var q = _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.Project.ProjectKey == projectKey && x.Project.OwnerKey == ownerKey);

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
            .Include(x => x.Results)
            .Skip(request.Offset)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        var items = runs.Select(run =>
        {
            var results = run.Results
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
                    ResponseSnippet = r.ResponseSnippet,
                    Classification = r.Classification ?? ResultClassification.Pass
                })
                .ToList();

            var summary = ResultClassificationRules.Summarize(results);

            return new TestRunRecord
            {
                RunId = run.RunId,
                OwnerKey = run.Project?.OwnerKey ?? ownerKey,
                ProjectKey = run.Project?.ProjectKey ?? projectKey,
                OperationId = run.OperationId,
                SpecId = run.SpecId,
                BaselineRunId = run.BaselineRunId,
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
                    ClassificationSummary = summary,
                    Results = new List<TestCaseResult>() // summaries don’t need case results
                }
            };
        }).ToList();

        var itemCount = items.Count;
        int? nextOffset = request.Offset + itemCount < total
            ? request.Offset + itemCount
            : null;

        return new PagedResult<TestRunRecord>(items, total, nextOffset);
    }

    private async Task<ProjectEntity> EnsureProjectAsync(string projectKey, string ownerKey)
    {
        var existing = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectKey == projectKey && p.OwnerKey == ownerKey);
        if (existing is not null) return existing;

        var created = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OwnerKey = ownerKey,
            ProjectKey = projectKey,
            Name = projectKey,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(created);
        await _db.SaveChangesAsync();
        return created;
    }
}
