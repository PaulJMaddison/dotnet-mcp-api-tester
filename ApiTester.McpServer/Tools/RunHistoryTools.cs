using System.ComponentModel;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RunHistoryTools
{
    private readonly ITestRunStore _store;

    public RunHistoryTools(ITestRunStore store)
    {
        _store = store;
    }

    [McpServerTool, Description("List recent test runs (most recent first).")]
    public async Task<object> ApiListRuns(int take = 20)
    {
        var runs = await _store.ListAsync(take);
        return new
        {
            take,
            total = runs.Count,
            runs = runs.Select(r => new
            {
                r.RunId,
                r.OperationId,
                r.StartedUtc,
                r.CompletedUtc,
                summary = new
                {
                    r.Result.TotalCases,
                    r.Result.Passed,
                    r.Result.Failed,
                    r.Result.Blocked,
                    r.Result.TotalDurationMs
                }
            })
        };
    }

    [McpServerTool, Description("Get a single test run by runId.")]
    public async Task<object> ApiGetRun(string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return new { isError = true, error = "Invalid runId, expected a GUID." };

        var run = await _store.GetAsync(id);
        if (run is null)
            return new { isError = true, error = $"Run not found: {runId}" };

        return run;
    }
}
