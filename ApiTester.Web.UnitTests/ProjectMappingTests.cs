using ApiTester.McpServer.Models;
using ApiTester.Web.Mapping;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class ProjectMappingTests
{
    [Fact]
    public void ToListResponse_MapsProjects()
    {
        var record = new ProjectRecord(Guid.NewGuid(), "Demo", "demo", DateTime.UtcNow);

        var response = ProjectMapping.ToListResponse(50, new[] { record });

        Assert.Equal(50, response.Take);
        Assert.Single(response.Projects);
        Assert.Equal(record.ProjectId, response.Projects[0].ProjectId);
    }

    [Fact]
    public void ToCreateResponse_MapsCreatedProject()
    {
        var record = new ProjectRecord(Guid.NewGuid(), "Demo", "demo", DateTime.UtcNow);

        var response = ProjectMapping.ToCreateResponse(record);

        Assert.Equal(record.ProjectId, response.ProjectId);
        Assert.Equal(record.Name, response.Name);
    }
}
