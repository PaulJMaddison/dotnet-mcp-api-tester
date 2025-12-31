using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class ProjectMapping
{
    public static ProjectResponse ToResponse(ProjectRecord record)
        => new(record.ProjectId, record.Name, record.ProjectKey, record.CreatedUtc);

    public static ProjectCreateResponse ToCreateResponse(ProjectRecord record)
        => new(record.ProjectId, record.Name, record.CreatedUtc);

    public static ProjectListResponse ToListResponse(int take, IReadOnlyList<ProjectRecord> records)
    {
        var projects = records.Select(ToResponse).ToList();
        return new ProjectListResponse(take, projects.Count, projects);
    }
}
