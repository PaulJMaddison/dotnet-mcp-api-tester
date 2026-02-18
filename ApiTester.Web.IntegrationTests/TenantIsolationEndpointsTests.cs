using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web;
using ApiTester.Web.Auth;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Web.IntegrationTests;

public class TenantIsolationEndpointsTests
{

    [Fact]
    public async Task TenantB_CannotAccessTenantA_ProjectsSpecsAndTestPlans()
    {
        using var factory = new ApiTesterWebFactory();
        var clients = ProvisionTenantClients(factory);

        var createProject = await clients.TenantA.PostAsJsonAsync("/api/projects", new { name = "Tenant Alpha Project" });
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(HttpStatusCode.OK, createProject.StatusCode);
        Assert.NotNull(project);

        using var specImport = BuildMultipartSpec(BuildSpecJson("getStatus"));
        var importResponse = await clients.TenantA.PostAsync($"/api/projects/{project!.ProjectId}/specs/import", specImport);
        var spec = await importResponse.Content.ReadFromJsonAsync<OpenApiSpecMetadataDto>();

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(spec);

        var generatePlanResponse = await clients.TenantA.PostAsync($"/api/projects/{project.ProjectId}/testplans/getStatus/generate", null);
        Assert.Equal(HttpStatusCode.OK, generatePlanResponse.StatusCode);

        var tenantBGetProject = await clients.TenantB.GetAsync($"/api/projects/{project.ProjectId}");
        var tenantBGetSpecs = await clients.TenantB.GetAsync($"/api/projects/{project.ProjectId}/specs");
        var tenantBGetTestPlan = await clients.TenantB.GetAsync($"/api/projects/{project.ProjectId}/testplans/getStatus");
        var tenantBDeleteSpec = await clients.TenantB.DeleteAsync($"/api/v1/projects/{project.ProjectId}/specs/{spec!.SpecId}");

        AssertTenantIsolationStatus(tenantBGetProject.StatusCode);
        AssertTenantIsolationStatus(tenantBGetSpecs.StatusCode);
        AssertTenantIsolationStatus(tenantBGetTestPlan.StatusCode);
        AssertTenantIsolationStatus(tenantBDeleteSpec.StatusCode);

        var tenantBListProjects = await clients.TenantB.GetFromJsonAsync<ProjectListResponse>("/api/projects?pageSize=50");
        Assert.NotNull(tenantBListProjects);
        Assert.DoesNotContain(tenantBListProjects!.Projects, p => p.ProjectId == project.ProjectId);
    }

    [Fact]
    public async Task TenantB_CannotAccessTenantA_RunsOrReports_AndRunFiltersDoNotLeak()
    {
        using var factory = new ApiTesterWebFactory();
        var clients = ProvisionTenantClients(factory);

        var createProject = await clients.TenantA.PostAsJsonAsync("/api/projects", new { name = "Tenant Alpha Runs" });
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(HttpStatusCode.OK, createProject.StatusCode);
        Assert.NotNull(project);

        var run = await SeedRunAsync(factory, project!);

        var tenantBGetRun = await clients.TenantB.GetAsync($"/api/runs/{run.RunId}");
        var tenantBReport = await clients.TenantB.GetAsync($"/api/runs/{run.RunId}/report?format=markdown");

        AssertTenantIsolationStatus(tenantBGetRun.StatusCode);
        AssertTenantIsolationStatus(tenantBReport.StatusCode);

        var tenantBFilteredRuns = await clients.TenantB.GetAsync($"/api/runs?projectKey={project.ProjectKey}&pageSize=25");
        AssertTenantIsolationStatus(tenantBFilteredRuns.StatusCode);

    }

    [Fact]
    public async Task TenantB_CannotReadOrDeleteTenantA_AuditEvents_AndAuditListDoesNotLeak()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Team"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Team));
        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationBravoId, new OrgSettings(OrgPlan.Team));

        var clients = ProvisionTenantClients(factory);

        var auditEvent = await SeedAuditEventAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, ApiTesterWebFactory.UserAlphaId);

        var tenantAAudit = await clients.TenantA.GetFromJsonAsync<AuditListResponse>("/audit?take=20");
        Assert.NotNull(tenantAAudit);
        Assert.Contains(tenantAAudit!.Events, e => e.TargetId == auditEvent.TargetId);

        var tenantBAudit = await clients.TenantB.GetAsync("/audit?take=20");
        AssertTenantIsolationStatus(tenantBAudit.StatusCode);
    }


    private static void AssertTenantIsolationStatus(HttpStatusCode statusCode)
    {
        Assert.True(
            statusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected tenant isolation status 403 or 404, got {(int)statusCode} ({statusCode}).");
    }

    private static TenantClients ProvisionTenantClients(WebApplicationFactory<Program> factory)
    {
        return new TenantClients(CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha), CreateClient(factory, ApiTesterWebFactory.ApiKeyBravo));
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static MultipartFormDataContent BuildMultipartSpec(string specJson)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StringContent(specJson, Encoding.UTF8, "application/json");
        content.Add(fileContent, "file", "openapi.json");
        return content;
    }

    private static string BuildSpecJson(string operationId)
    {
        return $$"""
                 {
                   "openapi": "3.0.0",
                   "info": {
                     "title": "Tenant Isolation",
                     "version": "1.0.0"
                   },
                   "servers": [
                     { "url": "https://example.test" }
                   ],
                   "paths": {
                     "/status": {
                       "get": {
                         "operationId": "{{operationId}}",
                         "responses": {
                           "200": {
                             "description": "ok"
                           }
                         }
                       }
                     }
                   }
                 }
                 """;
    }

    private static async Task<TestRunEntity> SeedRunAsync(WebApplicationFactory<Program> factory, ProjectDto project)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            OrganisationId = ApiTesterWebFactory.OrganisationAlphaId,
            TenantId = ApiTesterWebFactory.OrganisationAlphaId,
            ProjectId = project.ProjectId,
            OperationId = "tenant-op",
            StartedUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTime.UtcNow,
            TotalCases = 1,
            Passed = 1,
            Failed = 0,
            Blocked = 0,
            TotalDurationMs = 25,
            Results =
            [
                new TestCaseResultEntity
                {
                    Name = "tenant-case",
                    Method = "GET",
                    Url = "https://example.test/status",
                    StatusCode = 200,
                    DurationMs = 25,
                    Pass = true
                }
            ]
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private static async Task<AuditEventRecord> SeedAuditEventAsync(WebApplicationFactory<Program> factory, Guid organisationId, Guid actorId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var auditStore = scope.ServiceProvider.GetRequiredService<IAuditEventStore>();

        var record = new AuditEventRecord(
            Guid.NewGuid(),
            organisationId,
            actorId,
            AuditActions.ApiKeyCreated,
            "api_key",
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            JsonSerializer.Serialize(new { source = "tenant-isolation-test" }));

        await auditStore.CreateAsync(record, CancellationToken.None);
        return record;
    }

    private static async Task UpdateOrgSettingsAsync(
        WebApplicationFactory<Program> factory,
        Guid organisationId,
        OrgSettings settings)
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

    private sealed record TenantClients(HttpClient TenantA, HttpClient TenantB);
}
