using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Models;
using ApiTester.AI;
using ApiTester.Web;
using ApiTester.Web.Auth;
using ApiTester.Web.AI;
using ApiTester.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ApiTester.Web.IntegrationTests;

public class ApiEndpointsTests
{
    [Fact]
    public async Task GetProjects_ReturnsUnauthorized_WhenMissingApiKey()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_ReturnsForbidden_WhenOwnerMismatch()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "OwnerA", "owner-a");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyBravo);
        var response = await client.GetAsync($"/api/projects/{project.ProjectId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProjects_ReturnsProjects()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Alpha", "alpha");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetFromJsonAsync<ProjectListResponse>("/api/projects?pageSize=50");

        Assert.NotNull(response);
        Assert.Equal(50, response!.Metadata.PageSize);
        Assert.Contains(response.Projects, p => p.ProjectId == project.ProjectId);
    }

    [Fact]
    public async Task PostProjects_CreatesProject()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

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
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var first = await client.PostAsJsonAsync("/api/projects", new { name = "Echo" });
        var firstPayload = await first.Content.ReadFromJsonAsync<ProjectDto>();

        var second = await client.PostAsJsonAsync("/api/projects", new { name = "Echo" });
        var secondPayload = await second.Content.ReadFromJsonAsync<ProjectDto>();

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
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

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

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
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

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetFromJsonAsync<RunDetailDto>($"/api/runs/{run.RunId}");

        Assert.NotNull(response);
        Assert.Equal(run.RunId, response!.RunId);
    }

    [Fact]
    public async Task GetRun_ReturnsNotFound_WhenMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_ReturnsBadRequest_WhenNameMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportOpenApi_SavesSpecAndReturnsMetadata()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "OpenApi", "openapi");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var specJson = """
                       {
                         "openapi": "3.0.0",
                         "info": {
                           "title": "Sample API",
                           "version": "1.2.3"
                         },
                         "paths": {}
                       }
                       """;

        var content = BuildMultipartSpec(specJson);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/openapi/import", content);
        var payload = await response.Content.ReadFromJsonAsync<OpenApiSpecMetadataDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Sample API", payload!.Title);
        Assert.Equal("1.2.3", payload.Version);
        Assert.NotEqual(Guid.Empty, payload.SpecId);

        var fetched = await client.GetFromJsonAsync<OpenApiSpecMetadataDto>($"/api/projects/{project.ProjectId}/openapi");
        Assert.NotNull(fetched);
        Assert.Equal(payload.ProjectId, fetched!.ProjectId);
        Assert.Equal(payload.SpecId, fetched.SpecId);
    }

    [Fact]
    public async Task ImportOpenApi_ReturnsBadRequest_ForInvalidJson()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Broken", "broken");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var content = BuildMultipartSpec("not-json");
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/openapi/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportOpenApi_ReturnsNotFound_ForMissingProject()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var specJson = """
                       {
                         "openapi": "3.0.0",
                         "info": {
                           "title": "Missing",
                           "version": "0.1.0"
                         },
                         "paths": {}
                       }
                       """;

        var content = BuildMultipartSpec(specJson);
        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/openapi/import", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportOpenApi_ReturnsPayloadTooLarge_ForLargeSpec()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Large", "large");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var oversized = new string('a', OpenApiImportLimits.MaxSpecBytes);
        var specJson = $"{{\"openapi\":\"3.0.0\",\"info\":{{\"title\":\"Large\",\"version\":\"1.0\"}},\"x-notes\":\"{oversized}\"}}";

        var content = BuildMultipartSpec(specJson);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/openapi/import", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ImportOpenApi_ReturnsPayloadTooLarge_ForLargeRequestBody()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Oversized", "oversized");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var oversized = new string('a', OpenApiImportLimits.MaxRequestBodyBytes + 1024);
        var specJson = $"{{\"openapi\":\"3.0.0\",\"info\":{{\"title\":\"Large\",\"version\":\"1.0\"}},\"x-notes\":\"{oversized}\"}}";
        var content = BuildMultipartSpec(specJson);

        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/openapi/import", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_ReturnsPayloadTooLarge_ForLargeRequest()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var name = new string('a', RequestBodyLimits.MaxRequestBodyBytes + 1024);
        var json = JsonSerializer.Serialize(new { name });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/projects", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestPlan_ReturnsConflict_WhenNoSpecImported()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "NoSpec", "nospec");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/testplans/op-1/generate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestPlan_ReturnsNotFound_WhenOperationMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "MissingOp", "missingop");
        await SeedSpecAsync(factory, project, BuildSpecJson("op-1"));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/testplans/op-404/generate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestPlan_OverwritesExistingPlan()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Overwrite", "overwrite");
        await SeedSpecAsync(factory, project, BuildSpecJson("op-1"));

        var oldCreatedUtc = DateTime.UtcNow.AddHours(-1);
        await SeedTestPlanAsync(factory, project, "op-1", "old-plan", oldCreatedUtc);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/testplans/op-1/generate", null);
        var payload = await response.Content.ReadFromJsonAsync<TestPlanResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEqual("old-plan", payload!.PlanJson);
        Assert.True(payload.CreatedUtc > oldCreatedUtc);

        var fetched = await client.GetFromJsonAsync<TestPlanResponse>($"/api/projects/{project.ProjectId}/testplans/op-1");
        Assert.NotNull(fetched);
        Assert.Equal(payload.CreatedUtc, fetched!.CreatedUtc);
        Assert.Equal(payload.PlanJson, fetched.PlanJson);
    }

    [Fact]
    public async Task ExecuteTestPlan_RunsAndStoresResult_WithHttpbinSpec()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "RunNow", "runnow");
        var specJson = await File.ReadAllTextAsync(GetHttpbinSpecPath());
        await SeedSpecAsync(factory, project, specJson);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/getUuid", null);
        var payload = await response.Content.ReadFromJsonAsync<RunDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("getUuid", payload!.OperationId);
        Assert.NotEqual(Guid.Empty, payload.RunId);

        var storedRun = await client.GetFromJsonAsync<RunDetailDto>($"/api/runs/{payload.RunId}");
        Assert.NotNull(storedRun);

        var storedPlan = await client.GetFromJsonAsync<TestPlanResponse>($"/api/projects/{project.ProjectId}/testplans/getUuid");
        Assert.NotNull(storedPlan);

        Assert.True(payload.Result.TryGetProperty("results", out var results));
        Assert.Equal(JsonValueKind.Array, results.ValueKind);

        var pass = 0;
        var blocked = 0;
        var flaky = 0;
        var fail = 0;

        foreach (var item in results.EnumerateArray())
        {
            if (item.TryGetProperty("blocked", out var blockedEl) && blockedEl.ValueKind == JsonValueKind.True)
            {
                blocked++;
                continue;
            }

            if (item.TryGetProperty("pass", out var passEl) && passEl.ValueKind == JsonValueKind.True)
            {
                pass++;
                continue;
            }

            if (IsHttpbinFlake(item))
            {
                flaky++;
                continue;
            }

            fail++;
        }

        Assert.Equal(0, blocked);
        Assert.Equal(0, fail);
        Assert.True(pass + flaky > 0);
    }

    [Fact]
    public async Task ExecuteRun_BlocksLocalhost()
    {
        var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Execution:AllowedBaseUrls:0"] = "http://localhost:12345",
                    ["Execution:DryRun"] = "false"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "Blocked", "blocked");
        var specJson = BuildSpecJson("blockedOp", "http://localhost:12345", "/sample");
        await SeedSpecAsync(factory, project, specJson);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/blockedOp", null);
        var payload = await response.Content.ReadFromJsonAsync<RunDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Result.TryGetProperty("results", out var results));
        Assert.Contains(results.EnumerateArray(), item =>
            item.TryGetProperty("blocked", out var blockedEl) && blockedEl.ValueKind == JsonValueKind.True);
    }

    [Fact]
    public async Task ExecuteRun_ReportsTimeout()
    {
        var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Execution:AllowedBaseUrls:0"] = "https://httpbin.org",
                    ["Execution:TimeoutSeconds"] = "1",
                    ["Execution:DryRun"] = "false"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "Timeout", "timeout");
        var specJson = BuildSpecJson("delayOp", "https://httpbin.org", "/delay/3");
        await SeedSpecAsync(factory, project, specJson);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/projects/{project.ProjectId}/runs/execute/delayOp", null);
        var payload = await response.Content.ReadFromJsonAsync<RunDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Result.TryGetProperty("results", out var results));
        Assert.Contains(results.EnumerateArray(), item =>
        {
            if (!item.TryGetProperty("pass", out var passEl) || passEl.ValueKind != JsonValueKind.False)
                return false;

            if (!item.TryGetProperty("failureReason", out var failEl) || failEl.ValueKind != JsonValueKind.String)
                return false;

            var reason = failEl.GetString() ?? string.Empty;
            return reason.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("unreachable", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("dns", StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AiEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeAi", "free-ai");
        var run = await SeedRunAsync(factory, project, "op-free");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/ai/runs/{run.RunId}/explanation", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiEndpoint_ReturnsOk_ForProTier()
    {
        const string aiPayload = "{\"summary\":\"Run explanation.\"}";

        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiProvider>();
                services.AddSingleton<IAiProvider>(new FixedAiProvider(aiPayload));
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "ProAi", "pro-ai");
        var run = await SeedRunAsync(factory, project, "op-pro");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsync($"/api/ai/runs/{run.RunId}/explanation", null);
        var payload = await response.Content.ReadFromJsonAsync<AiRunExplanationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(run.RunId, payload!.RunId);
    }

    [Fact]
    public async Task AiSummariseRunEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeSummary", "free-summary");
        var run = await SeedRunAsync(factory, project, "op-free-summary");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/summarise-run", new AiSummariseRunRequest(run.RunId.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiComplianceReportEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeCompliance", "free-compliance");
        var run = await SeedRunAsync(factory, project, "op-free-compliance");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/compliance-report", new AiComplianceReportRequest(run.RunId.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiSummariseRunEndpoint_ReturnsNotFound_ForMissingRun()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/summarise-run", new AiSummariseRunRequest(Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AiSummariseRunEndpoint_ReturnsEvidenceRefs()
    {
        const string aiPayload = """
        {
          "overallSummary": "Run failed with a single 502 response; risk is moderate until upstream stability improves.",
          "topFailures": [
            {
              "title": "502 from pets endpoint",
              "evidenceRefs": [
                {
                  "caseName": "Failing Case",
                  "failureReason": "Expected 200 but got 502."
                }
              ]
            }
          ],
          "flakeAssessment": "Likely flaky external dependency due to 502 response.",
          "regressionLikelihood": {
            "level": "medium",
            "rationale": "Single failure suggests instability but not a systemic regression."
          },
          "recommendedNextActions": [
            "Re-run the failing case to confirm flakiness.",
            "Check upstream service logs for 502 spikes."
          ]
        }
        """;

        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiProvider>();
                services.AddSingleton<IAiProvider>(new FixedAiProvider(aiPayload));
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "SummaryEvidence", "summary-evidence");
        var run = await SeedFailingRunAsync(factory, project, "op-summary");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/summarise-run", new AiSummariseRunRequest(run.RunId.ToString()));
        var payload = await response.Content.ReadFromJsonAsync<AiRunSummaryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Single(payload!.TopFailures);
        Assert.Single(payload.TopFailures[0].EvidenceRefs);
        Assert.Equal("Failing Case", payload.TopFailures[0].EvidenceRefs[0].CaseName);
    }

    [Fact]
    public async Task AiComplianceReportEndpoint_ReturnsReport()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiClient>();
                services.AddSingleton<IAiClient>(new MockAiClient());
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "ComplianceReport", "compliance-report");
        var run = await SeedRunAsync(factory, project, "op-compliance");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/compliance-report", new AiComplianceReportRequest(run.RunId.ToString()));
        var payload = await response.Content.ReadFromJsonAsync<ComplianceReportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(run.RunId, payload!.RunId);
        Assert.Equal("mock", payload.Narrative);
        Assert.Equal(1, payload.RunResults.TotalCases);
    }

    [Fact]
    public async Task AiExplainEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeExplain", "free-explain");
        await SeedSpecAsync(factory, project, BuildExplainSpecJson());

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/explain", new AiExplainRequest(project.ProjectId.ToString(), "listPets"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiSuggestTestsEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeSuggest", "free-suggest");
        await SeedSpecAsync(factory, project, BuildExplainSpecJson());

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/suggest-tests", new AiSuggestTestsRequest(project.ProjectId.ToString(), "listPets"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiGenerateDocsEndpoint_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeDocs", "free-docs");
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.PostAsJsonAsync("/api/ai/generate-docs", new AiGenerateDocsRequest(project.ProjectId.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiGenerateDocsEndpoint_ReturnsConflict_ForMissingSpec()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "DocsEmpty", "docs-empty");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/generate-docs", new AiGenerateDocsRequest(project.ProjectId.ToString()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AiGenerateDocsEndpoint_RedactsRunExamples()
    {
        const string secretToken = "secret-token";
        var runId = Guid.NewGuid();

        var aiPayload = $$"""
        {
          "title": "Pet API docs",
          "summary": "Docs with redaction coverage.",
          "sections": [
            {
              "operationId": "listPets",
              "method": "GET",
              "path": "/pets",
              "title": "List pets",
              "summary": "Retrieve pets.",
              "markdown": "### GET /pets\nExample token: {{secretToken}}",
              "examples": [
                {
                  "title": "Success response",
                  "runId": "{{runId}}",
                  "caseName": "Case",
                  "statusCode": 200,
                  "responseSnippet": "token={{secretToken}}"
                }
              ]
            }
          ]
        }
        """;

        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiProvider>();
                services.AddSingleton<IAiProvider>(new FixedAiProvider(aiPayload));
            });
        });

        await UpdateOrgSettingsAsync(
            factory,
            ApiTesterWebFactory.OrganisationAlphaId,
            new OrgSettings(OrgPlan.Pro),
            new[] { secretToken });

        var project = await SeedProjectAsync(factory, "DocsRedaction", "docs-redaction");
        await SeedSpecAsync(factory, project, BuildExplainSpecJson());
        await SeedRunAsync(factory, project, "listPets", responseSnippet: $"token={secretToken}", runId: runId);

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/generate-docs", new AiGenerateDocsRequest(project.ProjectId.ToString()));
        var payload = await response.Content.ReadFromJsonAsync<GeneratedDocsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.DoesNotContain(secretToken, payload!.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", payload.Markdown, StringComparison.OrdinalIgnoreCase);

        var fetched = await client.GetFromJsonAsync<GeneratedDocsResponse>($"/api/projects/{project.ProjectId}/docs/generated");
        Assert.NotNull(fetched);
        Assert.DoesNotContain(secretToken, fetched!.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", fetched.Markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AiExplainEndpoint_ReturnsBadRequest_ForMissingOperationId()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "MissingExplainOp", "missing-explain-op");
        await SeedSpecAsync(factory, project, BuildExplainSpecJson());

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/explain", new AiExplainRequest(project.ProjectId.ToString(), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiExplainEndpoint_ReturnsUnprocessableEntity_ForInvalidSchema()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Pro"
                };
                config.AddInMemoryCollection(settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiProvider>();
                services.AddSingleton<IAiProvider>(new FixedAiProvider("{\"summary\":\"oops\"}"));
            });
        });

        await UpdateOrgSettingsAsync(factory, ApiTesterWebFactory.OrganisationAlphaId, new OrgSettings(OrgPlan.Pro));
        var project = await SeedProjectAsync(factory, "ExplainSchema", "explain-schema");
        await SeedSpecAsync(factory, project, BuildExplainSpecJson());

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.PostAsJsonAsync("/api/ai/explain", new AiExplainRequest(project.ProjectId.ToString(), "listPets"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RunReport_ReturnsForbidden_ForFreeTier()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Entitlements:Tier"] = "Free"
                };
                config.AddInMemoryCollection(settings);
            });
        });

        var project = await SeedProjectAsync(factory, "FreeReport", "free-report");
        var run = await SeedRunAsync(factory, project, "op-free-report");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/runs/{run.RunId}/report?format=markdown");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RunReport_ReturnsReport_ForTeamTier()
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
        var project = await SeedProjectAsync(factory, "ProReport", "pro-report");
        var run = await SeedRunAsync(factory, project, "op-pro-report");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/runs/{run.RunId}/report?format=markdown");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("# Test Run Report", body);
    }

    [Fact]
    public async Task RunReport_ReturnsHtmlFormat()
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
        var project = await SeedProjectAsync(factory, "HtmlReport", "html-report");
        var run = await SeedRunAsync(factory, project, "op-html");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/runs/{run.RunId}/report?format=html");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("<h1>Test Run Report</h1>", body);
    }

    [Fact]
    public async Task RunReport_ReturnsBadRequest_ForUnsupportedFormat()
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
        var project = await SeedProjectAsync(factory, "BadReport", "bad-report");
        var run = await SeedRunAsync(factory, project, "op-bad");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var response = await client.GetAsync($"/api/runs/{run.RunId}/report?format=pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RunExports_ReturnExpectedFormats()
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
        var project = await SeedProjectAsync(factory, "Exports", "exports");
        var run = await SeedRunAsync(factory, project, "op-export");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var junitResponse = await client.GetAsync($"/runs/{run.RunId}/export/junit");
        var junit = await junitResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, junitResponse.StatusCode);
        Assert.Contains("application/junit+xml", junitResponse.Content.Headers.ContentType?.ToString());
        Assert.Contains("<testsuite", junit);

        var jsonResponse = await client.GetAsync($"/runs/{run.RunId}/export/json");
        var json = await jsonResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Contains("application/json", jsonResponse.Content.Headers.ContentType?.ToString());
        using var jsonDoc = JsonDocument.Parse(json);
        Assert.True(jsonDoc.RootElement.TryGetProperty("runId", out _));

        var csvResponse = await client.GetAsync($"/runs/{run.RunId}/export/csv");
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Contains("text/csv", csvResponse.Content.Headers.ContentType?.ToString());
        Assert.StartsWith("case_name,status", csv, StringComparison.OrdinalIgnoreCase);

        var evidenceResponse = await client.GetAsync($"/runs/{run.RunId}/export/evidence-bundle");
        var evidenceBytes = await evidenceResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, evidenceResponse.StatusCode);
        Assert.Contains("application/zip", evidenceResponse.Content.Headers.ContentType?.ToString());

        using var stream = new MemoryStream(evidenceBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(entry => entry.FullName).ToList();
        Assert.Contains("run.json", entryNames);
        Assert.Contains("policy.json", entryNames);
        Assert.Contains("audit.json", entryNames);
        Assert.Contains("compliance-report.json", entryNames);
        Assert.Contains("exports/junit.xml", entryNames);
        Assert.Contains("exports/results.csv", entryNames);
    }

    [Fact]
    public async Task EvidenceBundle_RedactsSensitiveData()
    {
        const string secretToken = "secret-token";

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

        await UpdateOrgSettingsAsync(
            factory,
            ApiTesterWebFactory.OrganisationAlphaId,
            new OrgSettings(OrgPlan.Team),
            new[] { secretToken });

        var project = await SeedProjectAsync(factory, "EvidenceRedaction", "evidence-redaction");
        var run = await SeedRunAsync(factory, project, "op-redact", responseSnippet: $"token={secretToken}");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
        var evidenceResponse = await client.GetAsync($"/runs/{run.RunId}/export/evidence-bundle");
        var evidenceBytes = await evidenceResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, evidenceResponse.StatusCode);

        using var stream = new MemoryStream(evidenceBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var runEntry = archive.GetEntry("run.json");
        Assert.NotNull(runEntry);

        using var entryStream = runEntry!.Open();
        using var reader = new StreamReader(entryStream);
        var runJson = await reader.ReadToEndAsync();

        Assert.DoesNotContain(secretToken, runJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", runJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BaselineEndpoints_CreateListAndCompareRuns()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "Baselines", "baselines");
        var baselineRun = await SeedRunAsync(factory, project, "op-baseline");
        var run = await SeedRunAsync(factory, project, "op-baseline", DateTime.UtcNow.AddMinutes(-1));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var createResponse = await client.PostAsJsonAsync("/api/baselines", new BaselineCreateRequest(baselineRun.RunId));
        var createdBaseline = await createResponse.Content.ReadFromJsonAsync<BaselineDto>();
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(createdBaseline);
        Assert.Equal(baselineRun.RunId, createdBaseline!.RunId);

        var listResponse = await client.GetFromJsonAsync<BaselineListResponse>($"/api/baselines?projectKey={project.ProjectKey}&operationId=op-baseline&take=10");
        Assert.NotNull(listResponse);
        Assert.Contains(listResponse!.Baselines, baseline => baseline.RunId == baselineRun.RunId);

        var setResponse = await client.PostAsync($"/api/runs/{run.RunId}/baseline/{baselineRun.RunId}", null);
        Assert.Equal(HttpStatusCode.NoContent, setResponse.StatusCode);

        var compareResponse = await client.GetAsync($"/api/runs/{run.RunId}/compare/{baselineRun.RunId}");
        var comparePayload = await compareResponse.Content.ReadFromJsonAsync<RunComparisonResponse>();
        Assert.Equal(HttpStatusCode.OK, compareResponse.StatusCode);
        Assert.NotNull(comparePayload);
        Assert.Equal(baselineRun.RunId, comparePayload!.BaselineRunId);

        var compareToBaselineResponse = await client.GetAsync($"/api/runs/{run.RunId}/compare-to-baseline");
        var compareToBaselinePayload = await compareToBaselineResponse.Content.ReadFromJsonAsync<RunComparisonResponse>();
        Assert.Equal(HttpStatusCode.OK, compareToBaselineResponse.StatusCode);
        Assert.NotNull(compareToBaselinePayload);
        Assert.Equal(baselineRun.RunId, compareToBaselinePayload!.BaselineRunId);
    }

    [Theory]
    [InlineData("/api/runs/not-a-guid/compare/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("/api/runs/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/compare/not-a-guid")]
    [InlineData("/api/runs/not-a-guid/compare-to-baseline")]
    public async Task CompareEndpoints_ReturnBadRequest_ForInvalidIds(string url)
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompareEndpoints_ReturnNotFound_ForMissingRuns()
    {
        using var factory = new ApiTesterWebFactory();
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var compareResponse = await client.GetAsync($"/api/runs/{Guid.NewGuid()}/compare/{Guid.NewGuid()}");
        var compareBaselineResponse = await client.GetAsync($"/api/runs/{Guid.NewGuid()}/compare-to-baseline");

        Assert.Equal(HttpStatusCode.NotFound, compareResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, compareBaselineResponse.StatusCode);
    }

    [Fact]
    public async Task CompareEndpoints_ReturnNotFound_WhenBaselineMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var project = await SeedProjectAsync(factory, "MissingBaseline", "missing-baseline");
        var run = await SeedRunAsync(factory, project, "op-missing");

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var compareResponse = await client.GetAsync($"/api/runs/{run.RunId}/compare/{Guid.NewGuid()}");
        var compareBaselineResponse = await client.GetAsync($"/api/runs/{run.RunId}/compare-to-baseline");

        Assert.Equal(HttpStatusCode.NotFound, compareResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, compareBaselineResponse.StatusCode);
    }

    [Fact]
    public async Task GetProjects_PaginatesWithNextPageToken()
    {
        using var factory = new ApiTesterWebFactory();
        var first = await SeedProjectAsync(factory, "Alpha", "alpha", createdUtc: DateTime.UtcNow.AddMinutes(-5));
        var second = await SeedProjectAsync(factory, "Bravo", "bravo", createdUtc: DateTime.UtcNow.AddMinutes(-3));
        var third = await SeedProjectAsync(factory, "Charlie", "charlie", createdUtc: DateTime.UtcNow.AddMinutes(-1));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
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
        var project = await SeedProjectAsync(factory, "Echo", "echo", createdUtc: DateTime.UtcNow.AddHours(-1));
        var first = await SeedRunAsync(factory, project, "op-1", DateTime.UtcNow.AddMinutes(-10));
        var second = await SeedRunAsync(factory, project, "op-1", DateTime.UtcNow.AddMinutes(-5));

        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);
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
        var client = CreateClient(factory, ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            TenantId = organisationId ?? ApiTesterWebFactory.OrganisationAlphaId,
            OwnerKey = ownerKey,
            Name = name,
            ProjectKey = key,
            CreatedUtc = createdUtc ?? DateTime.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task UpdateOrgSettingsAsync(
        WebApplicationFactory<Program> factory,
        Guid organisationId,
        OrgSettings settings,
        IReadOnlyList<string>? redactionRules = null)
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
                Name = "Test Org",
                Slug = $"test-org-{organisationId:N}",
                CreatedUtc = DateTime.UtcNow
            };
            db.Organisations.Add(entity);
        }

        entity.OrgSettingsJson = JsonSerializer.Serialize(settings);
        if (redactionRules is not null)
            entity.RedactionRulesJson = JsonSerializer.Serialize(redactionRules);

        await db.SaveChangesAsync();
        await UpdateSubscriptionAsync(factory, organisationId, settings.Plan);
    }

    private static async Task UpdateSubscriptionAsync(
        WebApplicationFactory<Program> factory,
        Guid organisationId,
        OrgPlan plan)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var entity = await db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId);
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var subscriptionPlan = plan switch
        {
            OrgPlan.Pro => SubscriptionPlan.Pro,
            OrgPlan.Team => SubscriptionPlan.Team,
            _ => SubscriptionPlan.Free
        };

        if (entity is null)
        {
            entity = new SubscriptionEntity
            {
                OrganisationId = organisationId,
                Plan = subscriptionPlan,
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
            entity.Plan = subscriptionPlan;
            entity.Status = SubscriptionStatus.Active;
            entity.Renews = true;
            entity.PeriodStartUtc = periodStart;
            entity.PeriodEndUtc = periodEnd;
            entity.UpdatedUtc = now;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<TestRunEntity> SeedRunAsync(
        WebApplicationFactory<Program> factory,
        ProjectEntity project,
        string operationId,
        DateTime? startedUtc = null,
        string? responseSnippet = null,
        string? url = null,
        Guid? runId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var started = startedUtc ?? DateTime.UtcNow.AddMinutes(-2);

        var run = new TestRunEntity
        {
            RunId = runId ?? Guid.NewGuid(),
            OrganisationId = project.OrganisationId,
            TenantId = project.TenantId,
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
                    Url = url ?? "https://example.test",
                    StatusCode = 200,
                    DurationMs = 120,
                    Pass = true,
                    ResponseSnippet = responseSnippet
                }
            ]
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private static async Task<TestRunEntity> SeedFailingRunAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string operationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var started = DateTime.UtcNow.AddMinutes(-3);
        var run = new TestRunEntity
        {
            RunId = Guid.NewGuid(),
            OrganisationId = project.OrganisationId,
            TenantId = project.TenantId,
            ProjectId = project.ProjectId,
            OperationId = operationId,
            StartedUtc = started,
            CompletedUtc = started.AddMinutes(1),
            TotalCases = 1,
            Passed = 0,
            Failed = 1,
            Blocked = 0,
            TotalDurationMs = 100,
            Results =
            [
                new TestCaseResultEntity
                {
                    Name = "Failing Case",
                    Method = "GET",
                    Url = "https://example.test",
                    StatusCode = 502,
                    DurationMs = 100,
                    Pass = false,
                    FailureReason = "Expected 200 but got 502.",
                    ResponseSnippet = "Bad Gateway"
                }
            ]
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private static async Task<OpenApiSpecEntity> SeedSpecAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string specJson, DateTime? createdUtc = null)
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
            CreatedUtc = createdUtc ?? DateTime.UtcNow
        };

        db.OpenApiSpecs.Add(spec);
        await db.SaveChangesAsync();
        return spec;
    }

    private static async Task<TestPlanEntity> SeedTestPlanAsync(WebApplicationFactory<Program> factory, ProjectEntity project, string operationId, string planJson, DateTime? createdUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var plan = new TestPlanEntity
        {
            ProjectId = project.ProjectId,
            OperationId = operationId,
            PlanJson = planJson,
            CreatedUtc = createdUtc ?? DateTime.UtcNow
        };

        db.TestPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    private static MultipartFormDataContent BuildMultipartSpec(string specJson)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(specJson));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "openapi.json");
        return content;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        return client;
    }

    private static string BuildExplainSpecJson()
    {
        return """
        {
          "openapi": "3.0.1",
          "info": {
            "title": "Explain API",
            "version": "1.0.0"
          },
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "summary": "List pets",
                "parameters": [
                  {
                    "name": "limit",
                    "in": "query",
                    "schema": { "type": "integer" },
                    "example": 10
                  }
                ],
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "example": {
                          "pets": [
                            { "id": 1, "name": "Fluffy" }
                          ]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
    }

    private sealed class FixedAiProvider : IAiProvider
    {
        private readonly string _payload;

        public FixedAiProvider(string payload)
        {
            _payload = payload;
        }

        public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
            => Task.FromResult(new AiResult(_payload, "test-model"));
    }

    private static string GetHttpbinSpecPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ApiTester.McpClient",
            "Specs",
            "httpbin.openapi.json"));
    }

    private static bool IsHttpbinFlake(JsonElement resultItem)
    {
        if (resultItem.TryGetProperty("statusCode", out var scEl) &&
            scEl.ValueKind == JsonValueKind.Number &&
            scEl.GetInt32() == 502)
            return true;

        if (resultItem.TryGetProperty("responseSnippet", out var snipEl) && snipEl.ValueKind == JsonValueKind.String)
        {
            var snip = snipEl.GetString() ?? "";
            if (snip.Contains("502 Bad Gateway", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (resultItem.TryGetProperty("failureReason", out var failEl) && failEl.ValueKind == JsonValueKind.String)
        {
            var fail = failEl.GetString() ?? "";
            if (fail.Contains("but got 502", StringComparison.OrdinalIgnoreCase))
                return true;

            if (fail.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                fail.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
                fail.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                fail.Contains("unreachable", StringComparison.OrdinalIgnoreCase) ||
                fail.Contains("dns", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSpecJson(string operationId)
    {
        return $$"""
                 {
                   "openapi": "3.0.0",
                   "info": {
                     "title": "Sample API",
                     "version": "1.0.0"
                   },
                   "paths": {
                     "/sample": {
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

    private static string BuildSpecJson(string operationId, string serverUrl, string path)
    {
        return $$"""
                 {
                   "openapi": "3.0.0",
                   "info": {
                     "title": "Sample API",
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

    public sealed record OpenApiSpecMetadataDto(
        [property: JsonPropertyName("specId")] Guid SpecId,
        [property: JsonPropertyName("projectId")] Guid ProjectId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("specHash")] string SpecHash,
        [property: JsonPropertyName("uploadedUtc")] DateTime UploadedUtc);

    public sealed record TestPlanResponse(
        [property: JsonPropertyName("projectId")] Guid ProjectId,
        [property: JsonPropertyName("operationId")] string OperationId,
        [property: JsonPropertyName("planJson")] string PlanJson,
        [property: JsonPropertyName("createdUtc")] DateTime CreatedUtc);
}
