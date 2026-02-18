using System.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Auth;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace ApiTester.Web.IntegrationTests;

public sealed class BillingEnforcementEndpointsTests
{
    [Fact]
    public async Task BillingPlanAndUsage_ReturnFreeDefaults()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var planResponse = await client.GetAsync("/api/v1/billing/plan");
        var plan = await planResponse.Content.ReadFromJsonAsync<BillingPlanResponse>();

        var usageResponse = await client.GetAsync("/api/v1/billing/usage");
        var usage = await usageResponse.Content.ReadFromJsonAsync<BillingUsageResponse>();

        Assert.Equal(HttpStatusCode.OK, planResponse.StatusCode);
        Assert.NotNull(plan);
        Assert.Equal("Free", plan!.Plan);
        Assert.Equal(3, plan.Limits.MaxProjects);
        Assert.Equal(50, plan.Limits.MaxRunsPerPeriod);
        Assert.Equal(2, plan.Limits.MaxAiCallsPerPeriod);
        Assert.Equal(7, plan.RetentionDays);

        Assert.Equal(HttpStatusCode.OK, usageResponse.StatusCode);
        Assert.NotNull(usage);
        Assert.Equal(0, usage!.Usage.RunsUsed);
        Assert.Equal(0, usage.Usage.AiCallsUsed);
        Assert.Equal(0, usage.Usage.ExportsUsed);
    }


    [Fact]
    public async Task CreateProject_ReturnsPaymentRequired_WhenFreeProjectQuotaExceeded()
    {
        using var factory = new ApiTesterWebFactory();
        await SeedProjectAsync(factory, "One", "one");
        await SeedProjectAsync(factory, "Two", "two");
        await SeedProjectAsync(factory, "Three", "three");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/v1/projects", new ProjectCreateRequest("Four"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Project limit reached", problem!.Title);
    }

    [Fact]
    public async Task RunExecute_ReturnsPaymentRequired_WhenRunQuotaExceeded()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Runner", "runner");
        await ConsumeUsageAsync(factory, runsDelta: 50);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/op-list", null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Run quota exceeded", problem!.Title);
    }

    [Fact]
    public async Task RunDetail_ReturnsGone_WhenOutsideRetentionWindow()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Retention", "retention");
        var run = await SeedRunAsync(factory, project, "op-retained", DateTime.UtcNow.AddDays(-10));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/runs/{run.RunId}");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Retention window exceeded", problem!.Title);
    }

    [Fact]
    public async Task AiSuggestTests_ReturnsPaymentRequired_WhenAiQuotaExceeded()
    {
        using var factory = new ApiTesterWebFactory();
        await UpdateOrgPlanAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, OrgPlan.Pro);
        var project = await SeedProjectAsync(factory, "AiQuota", "ai-quota");
        await ConsumeUsageAsync(factory, aiCallsDelta: 200);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/suggest-tests", new
        {
            projectId = project.ProjectId.ToString(),
            operationId = "listPets"
        });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("AI quota exceeded", problem!.Title);
    }


    [Fact]
    public async Task EvidencePackExport_ReturnsForbidden_WhenPlanIsPro()
    {
        using var factory = new ApiTesterWebFactory();
        await UpdateOrgPlanAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, OrgPlan.Pro);
        var project = await SeedProjectAsync(factory, "EvidenceGate", "evidence-gate");
        var run = await SeedRunAsync(factory, project, "op-evidence", DateTime.UtcNow);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/v1/runs/{run.RunId}/evidence-pack");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Evidence pack export not available", problem!.Title);
    }

    [Fact]
    public async Task AuditLogEndpoint_ReturnsForbidden_WhenPlanIsPro()
    {
        using var factory = new ApiTesterWebFactory();
        await UpdateOrgPlanAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, OrgPlan.Pro);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync("/audit?take=10");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Audit log not available", problem!.Title);
    }


    [Fact]
    public async Task BillingWebhook_IsIdempotent_WhenSameEventPostedTwice()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        const string payload = """
        {
          "id":"evt_test_duplicate",
          "object":"event",
          "type":"customer.subscription.updated",
          "data":{
            "object":{
              "id":"sub_test_1",
              "object":"subscription",
              "customer":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "status":"active",
              "cancel_at_period_end":false,
              "current_period_start":1735689600,
              "current_period_end":1738368000,
              "items":{"data":[{"price":{"id":"price_pro","lookup_key":"pro"}}]},
              "metadata":{"tenantId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = CreateStripeSignatureHeader(timestamp, payload, "whsec_test_secret");

        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        firstRequest.Headers.Add("Stripe-Signature", signature);

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        secondRequest.Headers.Add("Stripe-Signature", signature);

        var first = await client.SendAsync(firstRequest);
        var second = await client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        return client;
    }

    private static async Task ConsumeUsageAsync(ApiTesterWebFactory factory, int runsDelta = 0, int aiCallsDelta = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionStore>();
        await subscriptions.TryConsumeAsync(
            ApiTesterWebFactory.OrganisationAlphaId,
            new SubscriptionUsageUpdate(0, runsDelta, aiCallsDelta, 0),
            new SubscriptionUsageLimits(int.MaxValue, int.MaxValue, int.MaxValue),
            DateTime.UtcNow,
            CancellationToken.None);
    }

    private static async Task UpdateOrgPlanAsync(WebApplicationFactory<Program> factory, Guid organisationId, OrgPlan plan)
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
                Name = "Billing Test Org",
                Slug = $"billing-{organisationId:N}",
                CreatedUtc = DateTime.UtcNow
            };

            db.Organisations.Add(entity);
        }

        entity.OrgSettingsJson = JsonSerializer.Serialize(new OrgSettings(plan));
        await db.SaveChangesAsync();
    }

    private static async Task<ProjectEntity> SeedProjectAsync(WebApplicationFactory<Program> factory, string name, string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OrganisationId = ApiTesterWebFactory.OrganisationAlphaId,
            TenantId = ApiTesterWebFactory.OrganisationAlphaId,
            OwnerKey = ApiTesterWebFactory.AlphaExternalId,
            Name = name,
            ProjectKey = key,
            CreatedUtc = DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task<TestRunEntity> SeedRunAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string operationId, DateTime startedUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            OrganisationId = project.OrganisationId,
            TenantId = project.TenantId,
            ProjectId = project.ProjectId,
            OperationId = operationId,
            StartedUtc = startedUtc,
            CompletedUtc = startedUtc.AddMinutes(1),
            TotalCases = 1,
            Passed = 1,
            Failed = 0,
            Blocked = 0,
            TotalDurationMs = 5,
            Results =
            [
                new TestCaseResultEntity
                {
                    Name = "ok",
                    Method = "GET",
                    Url = "https://example.test",
                    StatusCode = 200,
                    DurationMs = 5,
                    Pass = true
                }
            ]
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }


    private static string CreateStripeSignatureHeader(long timestamp, string payload, string secret)
    {
        var signed = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        return $"t={timestamp},v1={digest}";
    }

    private sealed record ProblemDetailsResponse(string? Title, string? Detail, int? Status);
}
