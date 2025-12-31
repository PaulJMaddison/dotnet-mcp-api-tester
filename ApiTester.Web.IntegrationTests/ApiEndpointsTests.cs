using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiTester.Web.IntegrationTests;

public class ApiEndpointsTests
{
    [Fact]
    public async Task GetProjects_ReturnsProjects()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Alpha", "alpha");

        var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?take=50");

        Assert.NotNull(response);
        Assert.Equal(50, response!.Take);
        Assert.Contains(response.Projects, p => p.ProjectId == project.ProjectId);
    }

    [Fact]
    public async Task PostProjects_CreatesProject()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Bravo" });
        var payload = await response.Content.ReadFromJsonAsync<ProjectCreateResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Bravo", payload!.Name);
        Assert.NotEqual(Guid.Empty, payload.ProjectId);
    }

    [Fact]
    public async Task GetProject_ReturnsNotFound_WhenMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRuns_FiltersByProjectAndOperation()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Gamma", "gamma");
        var runMatch = await SeedRunAsync(factory, project, "op-1");
        await SeedRunAsync(factory, project, "op-2");

        var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<RunSummaryResponse>($"/api/runs?projectKey={project.ProjectKey}&operationId=op-1&take=20");

        Assert.NotNull(response);
        Assert.Equal(1, response!.Runs.Count);
        Assert.Equal(runMatch.RunId, response.Runs[0].RunId);
    }

    [Fact]
    public async Task GetRun_ReturnsRun()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Delta", "delta");
        var run = await SeedRunAsync(factory, project, "op-1");

        var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{run.RunId}");

        Assert.NotNull(response);
        Assert.Equal(run.RunId, response!.RunId);
    }

    [Fact]
    public async Task GetRun_ReturnsNotFound_WhenMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_ReturnsBadRequest_WhenNameMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/projects?take=0")]
    [InlineData("/api/runs?projectKey=alpha&take=500")]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/not-a-guid")]
    [InlineData("/api/projects/not-a-guid")]
    public async Task InvalidRequests_ReturnBadRequest(string url)
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ProjectEntity> SeedProjectAsync(ApiTesterWebFactory factory, string name, string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            ProjectKey = key,
            CreatedUtc = DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task<TestRunEntity> SeedRunAsync(ApiTesterWebFactory factory, ProjectEntity project, string operationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            OperationId = operationId,
            StartedUtc = DateTime.UtcNow.AddMinutes(-2),
            CompletedUtc = DateTime.UtcNow,
            TotalCases = 1,
            Passed = 1,
            Failed = 0,
            Blocked = 0,
            TotalDurationMs = 120,
            Results =
            [
                new TestCaseResultEntity
                {
                    Name = "Case",
                    Method = "GET",
                    Url = "https://example.test",
                    StatusCode = 200,
                    DurationMs = 120,
                    Pass = true
                }
            ]
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    public sealed record ProjectListResponse(
        [property: JsonPropertyName("take")] int Take,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("projects")] IReadOnlyList<ProjectResponse> Projects);

    public sealed record ProjectResponse(
        [property: JsonPropertyName("projectId")] Guid ProjectId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("createdUtc")] DateTime CreatedUtc);

    public sealed record ProjectCreateResponse(
        [property: JsonPropertyName("projectId")] Guid ProjectId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("createdUtc")] DateTime CreatedUtc);

    public sealed record RunSummaryResponse(
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("take")] int Take,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("runs")] IReadOnlyList<RunSummaryItem> Runs);

    public sealed record RunSummaryItem(
        [property: JsonPropertyName("runId")] Guid RunId,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("operationId")] string OperationId,
        [property: JsonPropertyName("startedUtc")] DateTimeOffset StartedUtc,
        [property: JsonPropertyName("completedUtc")] DateTimeOffset CompletedUtc,
        [property: JsonPropertyName("summary")] JsonElement Summary);

    public sealed record RunDetailResponse(
        [property: JsonPropertyName("runId")] Guid RunId,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("operationId")] string OperationId,
        [property: JsonPropertyName("startedUtc")] DateTimeOffset StartedUtc,
        [property: JsonPropertyName("completedUtc")] DateTimeOffset CompletedUtc,
        [property: JsonPropertyName("result")] JsonElement Result);
}
