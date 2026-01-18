using ApiTester.McpServer.Models;
using ApiTester.Web.Mapping;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class ProjectMappingTests
{
    [Fact]
    public void ToListResponse_MapsProjects()
    {
        var record = new ProjectRecord(Guid.NewGuid(), OrgDefaults.DefaultOrganisationId, "Demo", "demo", DateTime.UtcNow);

        var metadata = new ApiTester.Web.Contracts.PageMetadata(10, 50, "next");
        var response = ProjectMapping.ToListResponse(metadata, new[] { record });

        Assert.Equal(metadata, response.Metadata);
        Assert.Single(response.Projects);
        Assert.Equal(record.ProjectId, response.Projects[0].ProjectId);
    }

    [Fact]
    public void ToCreateResponse_MapsCreatedProject()
    {
        var record = new ProjectRecord(Guid.NewGuid(), OrgDefaults.DefaultOrganisationId, "Demo", "demo", DateTime.UtcNow);

        var response = ProjectMapping.ToDto(record);

        Assert.Equal(record.ProjectId, response.ProjectId);
        Assert.Equal(record.Name, response.Name);
    }
}
