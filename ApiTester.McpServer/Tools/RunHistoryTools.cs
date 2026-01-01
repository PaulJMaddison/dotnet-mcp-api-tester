using System.ComponentModel;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RunHistoryTools
{
    private readonly ITestRunStore _store;
    private readonly ILogger<RunHistoryTools> _logger;

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

        _logger.LogInformation(
            "Listing runs for project {ProjectKey} with operation {OperationId} (take {Take})",
            projectKey,
            operationId ?? "(all)",
            take);

        // Day 14: store supports filtering (file store folder today, SQL WHERE later)
        var result = await _store.ListAsync(
            OwnerKeyDefaults.Default,
            projectKey,
            new PageRequest(take, 0),
            SortField.StartedUtc,
            SortDirection.Desc,
            operationId);

        return new
        {
            projectKey,
            pageSize = take,
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
            return new { isError = true, error = $"Run not found: {runId}" };

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
}
