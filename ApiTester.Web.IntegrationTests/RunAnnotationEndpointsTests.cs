using System.Net;
using System.Net.Http.Json;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Auth;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Web.IntegrationTests;

public class RunAnnotationEndpointsTests
{
    [Fact]
    public async Task AnnotationLifecycle_AllowsCreateUpdateDelete()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Annotations", "annotations");
        var run = await SeedRunAsync(factory, project, "op-annotate");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/runs/{run.RunId}/annotations",
            new RunAnnotationCreateRequest("Investigate flaky run", "https://jira.example.com/browse/TEST-1"));
        var created = await createResponse.Content.ReadFromJsonAsync<RunAnnotationDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(run.RunId, created!.RunId);
        Assert.Equal("Investigate flaky run", created.Note);
        Assert.Equal("https://jira.example.com/browse/TEST-1", created.JiraLink);

        var list = await client.GetFromJsonAsync<RunAnnotationListResponse>($"/api/runs/{run.RunId}/annotations");
        Assert.NotNull(list);
        Assert.Single(list!.Annotations);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/runs/{run.RunId}/annotations/{created.AnnotationId}",
            new RunAnnotationUpdateRequest("Resolved after retry", null));
        var updated = await updateResponse.Content.ReadFromJsonAsync<RunAnnotationDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Resolved after retry", updated!.Note);
        Assert.Null(updated.JiraLink);

        var deleteResponse = await client.DeleteAsync($"/api/runs/{run.RunId}/annotations/{created.AnnotationId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<RunAnnotationListResponse>($"/api/runs/{run.RunId}/annotations");
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete!.Annotations);
    }

    [Fact]
    public async Task CreateAnnotation_ReturnsBadRequest_WhenNoteMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "BadNote", "badnote");
        var run = await SeedRunAsync(factory, project, "op-badnote");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.PostAsJsonAsync(
            $"/api/runs/{run.RunId}/annotations",
            new RunAnnotationCreateRequest("  ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAnnotation_ReturnsBadRequest_WhenJiraLinkInvalid()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "BadJira", "badjira");
        var run = await SeedRunAsync(factory, project, "op-badjira");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.PostAsJsonAsync(
            $"/api/runs/{run.RunId}/annotations",
            new RunAnnotationCreateRequest("Check details", "ftp://jira.example.com/TIX-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        return client;
    }

    private static async Task<ProjectEntity> SeedProjectAsync(
        WebApplicationFactory<Program> factory,
        string name,
        string key,
        string ownerKey = ApiTesterWebFactory.AlphaExternalId,
        Guid? organisationId = null,
        DateTime? createdUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OrganisationId = organisationId ?? ApiTesterWebFactory.OrganisationAlphaId,
            OwnerKey = ownerKey,
            Name = name,
            ProjectKey = key,
            CreatedUtc = createdUtc ?? DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task<TestRunEntity> SeedRunAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string operationId, DateTime? startedUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var started = startedUtc ?? DateTime.UtcNow.AddMinutes(-2);

        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            OrganisationId = project.OrganisationId,
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
}
