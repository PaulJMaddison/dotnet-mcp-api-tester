using System.ComponentModel;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RunHistoryTools
{
    private readonly ITestRunStore _store;
    private readonly ILogger<RunHistoryTools> _logger;
    private const int DefaultTrendLimit = 25;

    public RunHistoryTools(ITestRunStore store, ILogger<RunHistoryTools> logger)
    {
        _store = store;
        _logger = logger;
    }

    [McpServerTool, Description("List recent test runs (most recent first). Optional filters: projectKey, operationId.")]
    public async Task<object> ApiListRuns(int take = 20, string? projectKey = null, string? operationId = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        var normalizedTake = Math.Clamp(take, 1, 200);

        _logger.LogInformation(
            "Listing runs for project {ProjectKey} with operation {OperationId} (take {Take})",
            projectKey,
            operationId ?? "(all)",
            normalizedTake);

        // Day 14: store supports filtering (file store folder today, SQL WHERE later)
        var result = await _store.ListAsync(
            OwnerKeyDefaults.Default,
            projectKey,
            new PageRequest(normalizedTake, 0),
            SortField.StartedUtc,
            SortDirection.Desc,
            operationId);

        return new
        {
            projectKey,
            pageSize = normalizedTake,
            total = result.Total,
            nextPageToken = result.NextOffset?.ToString(),
            runs = result.Items.Select(r => new
            {
                r.RunId,
                r.ProjectKey,
                r.OperationId,
                r.StartedUtc,
                r.CompletedUtc,
                summary = new
                {
                    r.Result.TotalCases,
                    r.Result.Passed,
                    r.Result.Failed,
                    r.Result.Blocked,
                    r.Result.TotalDurationMs,
                    r.Result.ClassificationSummary
                }
            })
        };
    }

    [McpServerTool, Description("Get a single test run by runId.")]
    public async Task<object> ApiGetRun(string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return new { isError = true, error = "Invalid runId, expected a GUID." };

        var run = await _store.GetAsync(OwnerKeyDefaults.Default, id);
        if (run is null)
            return new { run = (object?)null };

        return new
        {
            run.RunId,
            run.ProjectKey,
            run.OperationId,
            run.StartedUtc,
            run.CompletedUtc,
            run.Result
        };
    }

    [McpServerTool, Description("Get latency trends and regression analysis for an operation. Optional filters: projectKey, operationId, take, baselineRunId.")]
    public async Task<object> ApiLatencyTrends(
        string? operationId = null,
        string? projectKey = null,
        int take = DefaultTrendLimit,
        string? baselineRunId = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();

        Guid? baselineId = null;
        if (!string.IsNullOrWhiteSpace(baselineRunId))
        {
            if (Guid.TryParse(baselineRunId, out var parsed))
                baselineId = parsed;
            else
                return new { isError = true, error = "Invalid baselineRunId, expected a GUID." };
        }

        _logger.LogInformation(
            "Building latency trends for project {ProjectKey} operation {OperationId} (take {Take})",
            projectKey,
            operationId ?? "(all)",
            take);

        var result = await _store.ListAsync(
            OwnerKeyDefaults.Default,
            projectKey,
            new PageRequest(Math.Max(1, take), 0),
            SortField.StartedUtc,
            SortDirection.Desc,
            operationId);

        var runs = result.Items
            .OrderBy(r => r.StartedUtc)
            .ToList();

        var baseline = await ResolveBaselineAsync(projectKey, baselineId, runs);

        var summaries = runs.Select(run => new
        {
            run.RunId,
            run.OperationId,
            run.StartedUtc,
            run.CompletedUtc,
            snapshot = BuildLatencySnapshot(run)
        }).ToList();

        var currentSnapshot = summaries.LastOrDefault()?.snapshot;
        var baselineSnapshot = baseline is null ? null : BuildLatencySnapshot(baseline);

        var regression = LatencyAnalytics.EvaluateRegression(
            currentSnapshot,
            baselineSnapshot);

        return new
        {
            projectKey,
            operationId,
            baselineRunId = baseline?.RunId,
            sampleCount = summaries.Count,
            summaries,
            regression = new
            {
                regression.IsRegression,
                regression.Reason,
                regression.BaselineP95Ms,
                regression.CurrentP95Ms,
                regression.DeltaMs,
                regression.DeltaPercent
            }
        };
    }

    private static LatencySnapshot? BuildLatencySnapshot(TestRunRecord run)
    {
        if (run is null)
            return null;

        return LatencyAnalytics.BuildSnapshot(run.Result.Results.Select(r => r.DurationMs));
    }

    private async Task<TestRunRecord?> ResolveBaselineAsync(
        string projectKey,
        Guid? baselineId,
        List<TestRunRecord> runs)
    {
        if (baselineId.HasValue)
            return await _store.GetAsync(OwnerKeyDefaults.Default, baselineId.Value);

        var lastRun = runs.LastOrDefault();
        if (lastRun?.BaselineRunId is Guid storedBaselineId)
            return await _store.GetAsync(OwnerKeyDefaults.Default, storedBaselineId);

        return runs.Count > 1 ? runs[^2] : null;
    }
}
