using System.ComponentModel;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class TestPlanTools
{
    private readonly TestPlanRunner _runner;
    private readonly ILogger<TestPlanTools> _logger;

    public TestPlanTools(TestPlanRunner runner, ILogger<TestPlanTools> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    [McpServerTool, Description("Run a deterministic test plan for an operationId and return a stored run record.")]
    public async Task<object> ApiRunTestPlan(string operationId, string? projectKey = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();

        // Day 14: runner should persist with projectKey (file store folders today, SQL later)
        _logger.LogInformation("Running test plan for operation {OperationId} in project {ProjectKey}", operationId, projectKey);
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
