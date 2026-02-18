using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlRunStore : IRunStore
{
    private readonly ApiTesterDbContext _db;
    private readonly ILogger<SqlRunStore> _logger;

    public SqlRunStore(ApiTesterDbContext db, ILogger<SqlRunStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveAsync(Guid tenantId, Guid projectId, Guid runId, string operationId, DateTime startedUtc, DateTime completedUtc, TestRunResult result, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        _logger.LogInformation(
            "Saving run {RunId} for project {ProjectId} operation {OperationId}",
            runId,
            projectId,
            operationId);

        result.ClassificationSummary = ResultClassificationRules.Summarize(result.Results);

        var run = new TestRunEntity
        {
            RunId = runId,
            ProjectId = projectId,
            OrganisationId = tenantId,
            TenantId = tenantId,
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
                FailureDetailsJson = r.FailureDetails is null ? null : JsonSerializer.Serialize(r.FailureDetails, JsonDefaults.Default),
                ValidationUnavailableReason = r.ValidationUnavailableReason,
                ResponseSnippet = r.ResponseSnippet,
                IsFlaky = r.IsFlaky,
                FlakeReasonCategory = r.FlakeReasonCategory,
                Classification = r.Classification
            }).ToList()
        };

        _db.TestRuns.Add(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<object?> GetAsync(Guid tenantId, Guid runId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var run = await _db.TestRuns
            .AsNoTracking()
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.RunId == runId && x.TenantId == tenantId, ct);

        if (run is null) return null;

        var caseResults = run.Results
            .OrderBy(x => x.TestCaseResultId)
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
                FailureDetails = DeserializeFailureDetails(r.FailureDetailsJson),
                ValidationUnavailableReason = r.ValidationUnavailableReason,
                ResponseSnippet = r.ResponseSnippet,
                IsFlaky = r.IsFlaky,
                FlakeReasonCategory = r.FlakeReasonCategory
            })
            .ToList();

        var classificationSummary = ResultClassificationRules.Summarize(caseResults);

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
                classificationSummary,
                results = caseResults.Select(r => new
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
                    failureDetails = r.FailureDetails,
                    validationUnavailableReason = r.ValidationUnavailableReason,
                    responseSnippet = r.ResponseSnippet,
                    isFlaky = r.IsFlaky,
                    flakeReasonCategory = r.FlakeReasonCategory,
                    classification = r.Classification
                })
            }
        };
    }

    public async Task<object> ListAsync(Guid tenantId, Guid? projectId, int take, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        take = take <= 0 ? 20 : Math.Min(take, 200);

        var q = _db.TestRuns.AsNoTracking().Where(x => x.TenantId == tenantId);

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

    private static object? DeserializeFailureDetails(string? failureDetailsJson)
    {
        if (string.IsNullOrWhiteSpace(failureDetailsJson))
            return null;

        return JsonSerializer.Deserialize<object>(failureDetailsJson, JsonDefaults.Default);
    }

    private static Guid NormalizeTenantId(Guid tenantId)
        => tenantId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : tenantId;
}
