using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class ProjectToolsEdgeCaseTests
{
    [Fact]
    public async Task CreateProject_TrimsNameAndSetsCurrentContext()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var context = new ProjectContext();
            var tools = new ProjectTools(store, context, NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiCreateProject("   Interview Demo   ", CancellationToken.None);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
            var projectId = json.RootElement.GetProperty("projectId").GetGuid();
            var persisted = await store.GetAsync(OrgDefaults.DefaultOrganisationId, projectId, CancellationToken.None);

            Assert.NotNull(persisted);
            Assert.Equal("Interview Demo", persisted!.Name);
            Assert.Equal(projectId, context.CurrentProjectId);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task SetCurrentProject_AllZeroGuidIsRejectedWithoutChangingContext()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var context = new ProjectContext();
            var existing = Guid.NewGuid();
            context.SetCurrentProject(existing);
            var tools = new ProjectTools(store, context, NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiSetCurrentProject(Guid.Empty.ToString(), CancellationToken.None);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));

            Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(existing, context.CurrentProjectId);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ListProjects_TakeAtOrBelowZeroClampsToOne()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var tools = new ProjectTools(store, new ProjectContext(), NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiListProjects(0, CancellationToken.None);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));

            Assert.Equal(1, json.RootElement.GetProperty("pageSize").GetInt32());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"apitester-project-edge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDir(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
