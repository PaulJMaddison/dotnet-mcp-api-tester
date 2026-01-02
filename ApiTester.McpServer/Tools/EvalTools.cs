using System.ComponentModel;
using ApiTester.McpServer.Evals;
using ApiTester.McpServer.Runtime;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class EvalTools
{
    private readonly ProjectContext _ctx;
    private readonly EvalRunner _runner;

    public EvalTools(ProjectContext ctx, EvalRunner runner)
    {
        _ctx = ctx;
        _runner = runner;
    }

    [McpServerTool, Description("Run the demo evaluation suite over the indexed corpus and output a Markdown scorecard. If projectId is omitted, uses the current project.")]
    public async Task<object> ApiEvalRun(string? projectId = null, CancellationToken ct = default)
    {
        // Stateless, deterministic behaviour for demos
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!Guid.TryParse(projectId, out var pid))
                return new { ok = false, reason = "Invalid projectId GUID." };

            _ctx.SetCurrentProject(pid);
        }

        var current = _ctx.CurrentProjectId;
        if (current is null)
            return new { ok = false, reason = "No current project set. Pass projectId or call ApiSetCurrentProject / ApiCreateProject first." };

        // Works both in build output and when running from repo
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Evals", "evalset.demo.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ApiTester.McpServer", "Evals", "evalset.demo.json"))
        };

        var evalSetPath = candidates.FirstOrDefault(File.Exists);
        if (evalSetPath is null)
            return new { ok = false, reason = $"Eval set file not found. Tried: {string.Join(" | ", candidates)}" };

        // Persist reports so you can diff in demos
        var reportsDir = Path.Combine(AppContext.BaseDirectory, "Persistence", "Reports");
        Directory.CreateDirectory(reportsDir);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportPath = Path.Combine(reportsDir, $"eval-{current.Value:N}-{stamp}.md");

        var result = await _runner.RunAsync(current.Value, evalSetPath, ct);

        await File.WriteAllTextAsync(reportPath, result.Markdown, ct);

        return new
        {
            ok = true,
            projectId = current.Value,
            evalSetPath,
            overallScore = result.OverallScore,
            reportPath,
            markdown = result.Markdown
        };
    }
}
