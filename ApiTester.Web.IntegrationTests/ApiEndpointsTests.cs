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
        var response = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?pageSize=50");

        Assert.NotNull(response);
        Assert.Equal(50, response!.Metadata.PageSize);
        Assert.Contains(response.Projects, p => p.ProjectId == project.ProjectId);
    }

    [Fact]
    public async Task PostProjects_CreatesProject()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Bravo" });
        var payload = await response.Content.ReadFromJsonAsync<ProjectDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Bravo", payload!.Name);
        Assert.NotEqual(Guid.Empty, payload.ProjectId);
    }

    [Fact]
    public async Task PostProjects_ReturnsExisting_WhenProjectKeyExists()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/projects", new { name = "Echo" });
        var firstPayload = await first.Content.ReadFromJsonAsync<ProjectCreateResponse>();

        var second = await client.PostAsJsonAsync("/api/projects", new { name = "Echo" });
        var secondPayload = await second.Content.ReadFromJsonAsync<ProjectCreateResponse>();

        var list = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?take=10");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(firstPayload!.ProjectId, secondPayload!.ProjectId);
        Assert.NotNull(list);
        Assert.Equal(1, list!.Projects.Count);
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
        var response = await client.GetFromJsonAsync<RunSummaryResponse>($"/api/runs?projectKey={project.ProjectKey}&operationId=op-1&pageSize=20");

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
        var response = await client.GetFromJsonAsync<RunDetailDto>($"/api/runs/{run.RunId}");

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

    [Fact]
    public async Task GetProjects_PaginatesWithNextPageToken()
    {
        using var factory = new ApiTesterWebFactory();
        var first = await SeedProjectAsync(factory, "Alpha", "alpha", DateTime.UtcNow.AddMinutes(-5));
        var second = await SeedProjectAsync(factory, "Bravo", "bravo", DateTime.UtcNow.AddMinutes(-3));
        var third = await SeedProjectAsync(factory, "Charlie", "charlie", DateTime.UtcNow.AddMinutes(-1));

        var client = factory.CreateClient();
        var pageOne = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?pageSize=2");

        Assert.NotNull(pageOne);
        Assert.Equal(3, pageOne!.Metadata.Total);
        Assert.Equal(2, pageOne.Metadata.PageSize);
        Assert.Equal("2", pageOne.Metadata.NextPageToken);
        Assert.Equal(third.ProjectId, pageOne.Projects[0].ProjectId);
        Assert.Equal(second.ProjectId, pageOne.Projects[1].ProjectId);

        var pageTwo = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?pageSize=2&pageToken=2");

        Assert.NotNull(pageTwo);
        Assert.Null(pageTwo!.Metadata.NextPageToken);
        Assert.Single(pageTwo.Projects);
        Assert.Equal(first.ProjectId, pageTwo.Projects[0].ProjectId);
    }

    [Fact]
    public async Task GetRuns_SortsAndPaginates()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Echo", "echo", DateTime.UtcNow.AddHours(-1));
        var first = await SeedRunAsync(factory, project, "op-1", DateTime.UtcNow.AddMinutes(-10));
        var second = await SeedRunAsync(factory, project, "op-1", DateTime.UtcNow.AddMinutes(-5));

        var client = factory.CreateClient();
        var ascResponse = await client.GetFromJsonAsync<RunSummaryResponse>(
            $"/api/runs?projectKey={project.ProjectKey}&sort=startedUtc&order=asc&pageSize=10");

        Assert.NotNull(ascResponse);
        Assert.Equal(first.RunId, ascResponse!.Runs[0].RunId);
        Assert.Equal(second.RunId, ascResponse.Runs[1].RunId);

        var pagedResponse = await client.GetFromJsonAsync<RunSummaryResponse>(
            $"/api/runs?projectKey={project.ProjectKey}&pageSize=1");

        Assert.NotNull(pagedResponse);
        Assert.Equal("1", pagedResponse!.Metadata.NextPageToken);
        Assert.Single(pagedResponse.Runs);
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

    private static async Task<ProjectEntity> SeedProjectAsync(ApiTesterWebFactory factory, string name, string key, DateTime? createdUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            ProjectKey = key,
            CreatedUtc = createdUtc ?? DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task<TestRunEntity> SeedRunAsync(ApiTesterWebFactory factory, ProjectEntity project, string operationId, DateTime? startedUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var started = startedUtc ?? DateTime.UtcNow.AddMinutes(-2);

        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            OperationId = operationId,
            StartedUtc = started,
            CompletedUtc = started.AddMinutes(2),
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
        [property: JsonPropertyName("projects")] IReadOnlyList<ProjectDto> Projects,
        [property: JsonPropertyName("metadata")] PageMetadata Metadata);

    public sealed record PageMetadata(
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("pageSize")] int PageSize,
        [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

    public sealed record ProjectDto(
        [property: JsonPropertyName("projectId")] Guid ProjectId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("createdUtc")] DateTime CreatedUtc);

    public sealed record RunSummaryResponse(
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("runs")] IReadOnlyList<RunSummaryDto> Runs,
        [property: JsonPropertyName("metadata")] PageMetadata Metadata);

    public sealed record RunSummaryDto(
        [property: JsonPropertyName("runId")] Guid RunId,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("operationId")] string OperationId,
        [property: JsonPropertyName("startedUtc")] DateTimeOffset StartedUtc,
        [property: JsonPropertyName("completedUtc")] DateTimeOffset CompletedUtc,
        [property: JsonPropertyName("snapshot")] JsonElement Snapshot);

    public sealed record RunDetailDto(
        [property: JsonPropertyName("runId")] Guid RunId,
        [property: JsonPropertyName("projectKey")] string ProjectKey,
        [property: JsonPropertyName("operationId")] string OperationId,
        [property: JsonPropertyName("startedUtc")] DateTimeOffset StartedUtc,
        [property: JsonPropertyName("completedUtc")] DateTimeOffset CompletedUtc,
        [property: JsonPropertyName("result")] JsonElement Result);
}
