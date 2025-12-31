using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class ProjectMapping
{
    public static ProjectDto ToDto(ProjectRecord record)
        => new(record.ProjectId, record.Name, record.ProjectKey, record.CreatedUtc);

    public static ProjectListResponse ToListResponse(PageMetadata metadata, IReadOnlyList<ProjectRecord> records)
    {
        var projects = records.Select(ToDto).ToList();
        return new ProjectListResponse(projects, metadata);
    }
}
