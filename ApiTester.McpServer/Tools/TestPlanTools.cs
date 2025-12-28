using System.ComponentModel;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class TestPlanTools
{
    private readonly TestPlanRunner _runner;

    public TestPlanTools(TestPlanRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool, Description("Run a deterministic test plan for an operationId and return a stored run record.")]
    public async Task<object> ApiRunTestPlan(string operationId, string? projectKey = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        // Day 14: runner should persist with projectKey (file store folders today, SQL later)
        var record = await _runner.RunAsync(operationId, projectKey);

        return new
        {
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            record.Result
        };
    }
}
