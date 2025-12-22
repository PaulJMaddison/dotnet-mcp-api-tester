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

    [McpServerTool, Description("Run a deterministic test plan for an operationId and return pass/fail report.")]
    public async Task<object> ApiRunTestPlan(string operationId)
    {
        var record = await _runner.RunAsync(operationId);
        return record;
    }

}
