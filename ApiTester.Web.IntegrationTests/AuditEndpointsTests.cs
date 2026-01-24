using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web;
using ApiTester.Web.Auth;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiTester.Web.IntegrationTests;

public class AuditEndpointsTests
{
    [Fact]
    public async Task AuditLog_RecordsKeyOperations()
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

        await UpdateSubscriptionAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, SubscriptionPlan.Team);
        var project = await SeedProjectAsync(factory, "Audit", "audit");
        await SeedSpecAsync(factory, project, BuildSpecJson("getUuid", "https://httpbin.org", "/uuid"));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var createResponse = await client.PostAsJsonAsync("/api-keys", new ApiKeyCreateRequest(
            "Audit Key",
            new List<string> { ApiKeyScopes.ProjectsRead },
            null));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdPayload = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreateResponse>();
        Assert.NotNull(createdPayload);

        var revokeResponse = await client.PostAsync($"/api-keys/{createdPayload!.ApiKey.KeyId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var runResponse = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/getUuid", null);
        var runPayload = await runResponse.Content.ReadFromJsonAsync<RunDetailDto>();
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.NotNull(runPayload);

        var reportResponse = await client.GetAsync($"/api/runs/{runPayload!.RunId}/report?format=markdown");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var auditResponse = await client.GetFromJsonAsync<AuditListResponse>("/audit?take=20");

        Assert.NotNull(auditResponse);
        var actions = auditResponse!.Events.Select(e => e.Action).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AuditActions.ApiKeyCreated, actions);
        Assert.Contains(AuditActions.ApiKeyRevoked, actions);
        Assert.Contains(AuditActions.RunExecuted, actions);
        Assert.Contains(AuditActions.ExportGenerated, actions);
    }

    [Fact]
    public async Task AuditLog_FiltersByActionAndTimestamp()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        await using var scope = factory.Services.CreateAsyncScope();
        var auditStore = scope.ServiceProvider.GetRequiredService<IAuditEventStore>();

        var orgId = ApiTesterWebFactory.OrganisationAlphaId;
        var actorId = ApiTesterWebFactory.UserAlphaId;

        await auditStore.CreateAsync(new AuditEventRecord(
            Guid.NewGuid(),
            orgId,
            actorId,
            AuditActions.ApiKeyCreated,
            "api_key",
            Guid.NewGuid().ToString(),
            DateTime.UtcNow.AddDays(-2),
            null),
            CancellationToken.None);

        await auditStore.CreateAsync(new AuditEventRecord(
            Guid.NewGuid(),
            orgId,
            actorId,
            AuditActions.ApiKeyCreated,
            "api_key",
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            null),
            CancellationToken.None);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var response = await client.GetFromJsonAsync<AuditListResponse>($"/audit?take=10&action={AuditActions.ApiKeyCreated}&from={from}");

        Assert.NotNull(response);
        Assert.Single(response!.Events);
        Assert.Equal(AuditActions.ApiKeyCreated, response.Events[0].Action);
        Assert.True(response.Events[0].CreatedUtc >= DateTime.UtcNow.AddHours(-1));
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
        Guid? organisationId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OrganisationId = organisationId ?? ApiTesterWebFactory.OrganisationAlphaId,
            TenantId = organisationId ?? ApiTesterWebFactory.OrganisationAlphaId,
            OwnerKey = ownerKey,
            Name = name,
            ProjectKey = key,
            CreatedUtc = DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task UpdateSubscriptionAsync(
        WebApplicationFactory<Program> factory,
        Guid organisationId,
        SubscriptionPlan plan)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var entity = await db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId);
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        if (entity is null)
        {
            entity = new SubscriptionEntity
            {
                OrganisationId = organisationId,
                Plan = plan,
                Status = SubscriptionStatus.Active,
                Renews = true,
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd,
                ProjectsUsed = 0,
                RunsUsed = 0,
                AiCallsUsed = 0,
                UpdatedUtc = now
            };
            db.Subscriptions.Add(entity);
        }
        else
        {
            entity.Plan = plan;
            entity.Status = SubscriptionStatus.Active;
            entity.Renews = true;
            entity.PeriodStartUtc = periodStart;
            entity.PeriodEndUtc = periodEnd;
            entity.UpdatedUtc = now;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<OpenApiSpecEntity> SeedSpecAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string specJson)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var spec = new OpenApiSpecEntity
        {
            SpecId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            TenantId = project.TenantId,
            Title = "Spec",
            Version = "1.0",
            SpecJson = specJson,
            CreatedUtc = DateTime.UtcNow
        };

        db.OpenApiSpecs.Add(spec);
        await db.SaveChangesAsync();
        return spec;
    }

    private static string BuildSpecJson(string operationId, string serverUrl, string path)
    {
        return $$"""
                 {
                   "openapi": "3.0.0",
                   "info": {
                     "title": "Audit API",
                     "version": "1.0.0"
                   },
                   "servers": [
                     { "url": "{{serverUrl}}" }
                   ],
                   "paths": {
                     "{{path}}": {
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

    public sealed record RunDetailDto(
        Guid RunId,
        string ProjectKey,
        string OperationId,
        DateTimeOffset StartedUtc,
        DateTimeOffset CompletedUtc,
        JsonElement Result);
}
