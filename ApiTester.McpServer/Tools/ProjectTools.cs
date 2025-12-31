using System.ComponentModel;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class ProjectTools
{
    private readonly IProjectStore _projects;
    private readonly ProjectContext _ctx;

    public ProjectTools(IProjectStore projects, ProjectContext ctx)
    {
        _projects = projects;
        _ctx = ctx;
    }

    [McpServerTool, Description("Create a new project. Returns projectId.")]
    public async Task<object> ApiCreateProject(string name, CancellationToken ct)
    {
        var project = await _projects.CreateAsync(name, ct);
        _ctx.SetCurrentProject(project.ProjectId);
        return new { projectId = project.ProjectId, currentProjectId = project.ProjectId };
    }

    [McpServerTool, Description("List recent projects.")]
    public async Task<object> ApiListProjects(int take = 50, CancellationToken ct = default)
    {
        var projects = await _projects.ListAsync(take, ct);
        return new { take, total = projects.Count, projects };
    }

    [McpServerTool, Description("Set the current project used for storing runs.")]
    public async Task<object> ApiSetCurrentProject(string projectId, CancellationToken ct)
    {
        if (!Guid.TryParse(projectId, out var id))
            return new { ok = false, reason = "Invalid projectId GUID." };

        if (await _projects.GetAsync(id, ct) is null)
            return new { ok = false, reason = "Project not found." };

        _ctx.SetCurrentProject(id);
        return new { ok = true, currentProjectId = id };
    }

    [McpServerTool, Description("Get the current projectId.")]
    public Task<object> ApiGetCurrentProject(CancellationToken ct)
    {
        return Task.FromResult<object>(new { currentProjectId = _ctx.CurrentProjectId });
    }
}
