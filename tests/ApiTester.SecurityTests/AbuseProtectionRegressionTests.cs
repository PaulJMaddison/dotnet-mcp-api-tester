using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Contracts;
using ApiTester.Web.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.SecurityTests;

public sealed class AbuseProtectionRegressionTests
{
    [Fact(DisplayName = "ABUSE-01 evidence pack export is gated for non-Team subscriptions")]
    public async Task Abuse01_EvidencePackExportIsGated()
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

        using var client = CreateApiKeyClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var projectId = await CreateProjectWithSpecAndRunAsync(factory, client, fixtureApi.BaseUrl);

        var runsResponse = await client.GetAsync($"/api/runs?projectKey={projectId}");
        runsResponse.EnsureSuccessStatusCode();
        var list = await runsResponse.Content.ReadFromJsonAsync<RunSummaryResponse>();
        var runId = Assert.Single(list!.Runs).RunId;

        var exportResponse = await client.GetAsync($"/runs/{runId}/export/evidence-bundle");
        Assert.True(exportResponse.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "ABUSE-02 OpenAPI import size cap is enforced")]
    public async Task Abuse02_OpenApiImportSizeCapEnforced()
    {
        using var factory = new ApiTesterWebFactory();
        using var client = CreateApiKeyClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var createProjectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Security Oversize Project" });
        createProjectResponse.EnsureSuccessStatusCode();
        var project = await createProjectResponse.Content.ReadFromJsonAsync<ProjectDto>();

        using var form = new MultipartFormDataContent();
        var largeSpec = new string('x', 1_200_000);
        form.Add(new StringContent(largeSpec, Encoding.UTF8, "application/json"), "file", "oversize-openapi.json");

        var response = await client.PostAsync($"/api/projects/{project!.ProjectId}/specs/import", form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(payload.Contains("OpenAPI spec too large", StringComparison.OrdinalIgnoreCase) || payload.Contains("Request too large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "ABUSE-03 tenant/IP limiter enforces quota and returns retry")]
    public void Abuse03_RateLimiterBlocksExcessRequests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ApiTester.Web.AbuseProtection.AbuseRateLimitOptions
        {
            Default = new ApiTester.Web.AbuseProtection.EndpointRateLimitPolicy(1, 1)
        });

        var limiter = new ApiTester.Web.AbuseProtection.TenantIpRateLimiter(options, TimeProvider.System);
        var tenantId = Guid.NewGuid();

        var first = limiter.TryConsume(tenantId, "203.0.113.10", ApiTester.Web.AbuseProtection.EndpointCategory.Default, out var retryAfterFirst);
        var second = limiter.TryConsume(tenantId, "203.0.113.10", ApiTester.Web.AbuseProtection.EndpointCategory.Default, out var retryAfterSecond);

        Assert.True(first);
        Assert.Equal(TimeSpan.Zero, retryAfterFirst);
        Assert.False(second);
        Assert.True(retryAfterSecond.TotalSeconds >= 1);
    }

    private static HttpClient CreateApiKeyClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static async Task<string> CreateProjectWithSpecAndRunAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> factory, HttpClient client, string baseUrl)
    {
        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Free));

        var createProjectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Security Export Gate" });
        createProjectResponse.EnsureSuccessStatusCode();
        var project = await createProjectResponse.Content.ReadFromJsonAsync<ProjectDto>();

        var specJson = $$"""
        {
          "openapi": "3.0.1",
          "info": { "title": "fixture", "version": "1.0.0" },
          "servers": [ { "url": "{{baseUrl}}" } ],
          "paths": {
            "/fixture/status": {
              "get": {
                "operationId": "getFixtureStatus",
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;

        using var importContent = new MultipartFormDataContent();
        importContent.Add(new StringContent(specJson, Encoding.UTF8, "application/json"), "file", "security-openapi.json");
        var importResponse = await client.PostAsync($"/api/projects/{project!.ProjectId}/specs/import", importContent);
        importResponse.EnsureSuccessStatusCode();

        var executeResponse = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/getFixtureStatus", null);
        executeResponse.EnsureSuccessStatusCode();

        return project.ProjectKey;
    }

    private static async Task UpdateOrgSettingsAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> factory, Guid organisationId, OrgSettings settings)
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

        entity.OrgSettingsJson = System.Text.Json.JsonSerializer.Serialize(settings);
        await db.SaveChangesAsync();
    }
}
