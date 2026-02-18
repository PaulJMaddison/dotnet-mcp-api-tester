using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Web.IntegrationTests;

public sealed class SmokeFlowTests
{
    [Fact]
    public async Task SmokeFlow_CreatesTokenProjectPlanRunAndEvidenceBundle_UsingFixtureApi()
    {
        await using var fixtureApi = await FixtureApiHost.StartAsync();

        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Execution:AllowedBaseUrls:0"] = fixtureApi.BaseUrl,
                    ["Execution:BlockLocalhost"] = "false",
                    ["Execution:BlockPrivateNetworks"] = "false"
                });
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Team));
        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationBravoId, new OrgSettings(OrgPlan.Team));

        using var adminClient = CreateApiKeyClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var tokenResponse = await adminClient.PostAsJsonAsync("/api/v1/tokens", new ApiKeyCreateRequest(
            "smoke-token",
            new List<string>
            {
                "projects:read",
                "projects:write",
                "runs:read",
                "runs:write"
            },
            null));

        tokenResponse.EnsureSuccessStatusCode();
        var createdToken = await tokenResponse.Content.ReadFromJsonAsync<ApiKeyCreateResponse>();
        Assert.NotNull(createdToken);

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", createdToken!.Token);

        var createProjectResponse = await tokenClient.PostAsJsonAsync("/api/projects", new { name = "Smoke Tenant Alpha Project" });
        createProjectResponse.EnsureSuccessStatusCode();
        var project = await createProjectResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        var specTemplate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "smoke-openapi.json"));
        var specJson = specTemplate.Replace("__BASE_URL__", fixtureApi.BaseUrl, StringComparison.Ordinal);

        using var importContent = new MultipartFormDataContent();
        importContent.Add(new StringContent(specJson, Encoding.UTF8, "application/json"), "file", "smoke-openapi.json");
        var importResponse = await tokenClient.PostAsync($"/api/projects/{project!.ProjectId}/specs/import", importContent);
        importResponse.EnsureSuccessStatusCode();

        var generateResponse = await tokenClient.PostAsync($"/api/projects/{project.ProjectId}/testplans/getFixtureStatus/generate", null);
        generateResponse.EnsureSuccessStatusCode();

        var executeResponse = await tokenClient.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/getFixtureStatus", null);
        executeResponse.EnsureSuccessStatusCode();
        var run = await executeResponse.Content.ReadFromJsonAsync<RunDetailDto>();
        Assert.NotNull(run);

        var storedRun = await tokenClient.GetAsync($"/api/runs/{run!.RunId}");
        Assert.Equal(HttpStatusCode.OK, storedRun.StatusCode);

        var evidenceResponse = await tokenClient.GetAsync($"/runs/{run.RunId}/export/evidence-bundle");
        evidenceResponse.EnsureSuccessStatusCode();
        var evidenceBytes = await evidenceResponse.Content.ReadAsByteArrayAsync();

        using var evidenceStream = new MemoryStream(evidenceBytes);
        using var archive = new ZipArchive(evidenceStream, ZipArchiveMode.Read);
        var names = archive.Entries.Select(x => x.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("manifest.json", names);
        Assert.Contains("run.json", names);

        using var bravoClient = CreateApiKeyClient(factory, ApiTesterWebFactory.ApiKeyBravo);
        var crossTenantResponse = await bravoClient.GetAsync($"/api/projects/{project.ProjectId}");
        Assert.True(crossTenantResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    private static HttpClient CreateApiKeyClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static async Task UpdateOrgSettingsAsync(WebApplicationFactory<Program> factory, Guid organisationId, OrgSettings settings)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var entity = await db.Organisations.FirstOrDefaultAsync(o => o.OrganisationId == organisationId);
        if (entity is null)
        {
            entity = new OrganisationEntity
            {
                OrganisationId = organisationId,
                Name = $"Org-{organisationId:N}",
                Slug = $"org-{organisationId:N}",
                CreatedUtc = DateTime.UtcNow
            };
            db.Organisations.Add(entity);
        }

        entity.OrgSettingsJson = JsonSerializer.Serialize(settings);
        await db.SaveChangesAsync();
    }
}
