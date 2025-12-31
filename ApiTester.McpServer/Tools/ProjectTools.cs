using System.ComponentModel;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class ProjectTools
{
    private readonly IProjectStore _projects;
    private readonly ProjectContext _ctx;
    private readonly ILogger<ProjectTools> _logger;

    public ProjectTools(IProjectStore projects, ProjectContext ctx, ILogger<ProjectTools> logger)
    {
        _projects = projects;
        _ctx = ctx;
        _logger = logger;
    }

    [McpServerTool, Description("Create a new project. Returns projectId.")]
    public async Task<object> ApiCreateProject(string name, CancellationToken ct)
    {
        var project = await _projects.CreateAsync(OwnerKeyDefaults.Default, name, ct);
        _ctx.SetCurrentProject(project.ProjectId);
        _logger.LogInformation("Created project {ProjectId} with name {ProjectName}", project.ProjectId, project.Name);
        return new { projectId = project.ProjectId, currentProjectId = project.ProjectId };
    }

    [McpServerTool, Description("List recent projects.")]
    public async Task<object> ApiListProjects(int take = 50, CancellationToken ct = default)
    {
        var result = await _projects.ListAsync(OwnerKeyDefaults.Default, new PageRequest(take, 0), SortField.CreatedUtc, SortDirection.Desc, ct);
        return new
        {
            pageSize = take,
            total = result.Total,
            nextPageToken = result.NextOffset?.ToString(),
            projects = result.Items
        };
    }

    [McpServerTool, Description("Set the current project used for storing runs.")]
    public async Task<object> ApiSetCurrentProject(string projectId, CancellationToken ct)
    {
        if (!Guid.TryParse(projectId, out var id))
            return new { ok = false, reason = "Invalid projectId GUID." };

        if (await _projects.GetAsync(OwnerKeyDefaults.Default, id, ct) is null)
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
