using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using ApiTester.Web.AI;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AiSuggestTestsServiceTests
{
    [Fact]
    public async Task SuggestAsync_RejectsInvalidJson()
    {
        var provider = new CapturingAiProvider(_ => new AiResult("{\"cases\":[{\"name\":\"oops\"}]}", "test-model"));
        var draftStore = new InMemoryDraftStore();
        var rateLimiter = new AiRateLimiter(
            Options.Create(new AiRateLimitOptions { Capacity = 10, RefillTokensPerMinute = 10 }),
            TimeProvider.System);
        var service = new AiSuggestTestsService(provider, new RedactionService(), draftStore, rateLimiter, TimeProvider.System);
        var input = BuildInput();

        await Assert.ThrowsAsync<AiSchemaValidationException>(() => service.SuggestAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task SuggestAsync_ConvertsSuggestionsToPlan()
    {
        var json = """
        {
          "cases": [
            {
              "name": "Missing auth",
              "rationale": "Should reject",
              "params": {
                "path": { "id": "123" },
                "query": { "limit": "1" },
                "headers": { "Authorization": "" }
              },
              "expectedStatusRanges": ["401-403", "400"]
            }
          ]
        }
        """;

        var provider = new CapturingAiProvider(_ => new AiResult(json, "test-model"));
        var draftStore = new InMemoryDraftStore();
        var rateLimiter = new AiRateLimiter(
            Options.Create(new AiRateLimitOptions { Capacity = 10, RefillTokensPerMinute = 10 }),
            TimeProvider.System);
        var service = new AiSuggestTestsService(provider, new RedactionService(), draftStore, rateLimiter, TimeProvider.System);
        var input = BuildInput();

        var result = await service.SuggestAsync(input, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Draft.DraftId);
        var plan = JsonSerializer.Deserialize<TestPlan>(result.Draft.PlanJson, JsonDefaults.Default);
        Assert.NotNull(plan);
        Assert.Equal("listPets", plan!.OperationId);
        Assert.Single(plan.Cases);
        Assert.Equal("Missing auth — Should reject", plan.Cases[0].Name);
        Assert.Equal("123", plan.Cases[0].PathParams["id"]);
        Assert.Equal("1", plan.Cases[0].QueryParams["limit"]);
        Assert.Contains(401, plan.Cases[0].ExpectedStatusCodes);
        Assert.Contains(402, plan.Cases[0].ExpectedStatusCodes);
        Assert.Contains(403, plan.Cases[0].ExpectedStatusCodes);
        Assert.Contains(400, plan.Cases[0].ExpectedStatusCodes);
    }

    private static AiSuggestTestsInput BuildInput()
    {
        var org = new OrganisationRecord(
            Guid.NewGuid(),
            "AI Org",
            "ai-org",
            DateTime.UtcNow,
            orgSettings: new OrgSettings(OrgPlan.Pro));

        var operation = new OpenApiOperation
        {
            Summary = "List pets",
            Parameters = new List<OpenApiParameter>
            {
                new()
                {
                    Name = "id",
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = "string" }
                },
                new()
                {
                    Name = "limit",
                    In = ParameterLocation.Query,
                    Required = false,
                    Schema = new OpenApiSchema { Type = "integer" }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Ok" }
            }
        };

        return new AiSuggestTestsInput(org, Guid.NewGuid(), "listPets", "GET", "/pets/{id}", operation);
    }

    private sealed class CapturingAiProvider : IAiProvider
    {
        private readonly Func<AiRequest, AiResult> _responder;

        public CapturingAiProvider(Func<AiRequest, AiResult> responder)
        {
            _responder = responder;
        }

        public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    private sealed class InMemoryDraftStore : ITestPlanDraftStore
    {
        private readonly List<TestPlanDraftRecord> _records = new();

        public Task<TestPlanDraftRecord?> GetAsync(Guid draftId, CancellationToken ct)
            => Task.FromResult(_records.FirstOrDefault(r => r.DraftId == draftId));

        public Task<TestPlanDraftRecord> CreateAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct)
        {
            var record = new TestPlanDraftRecord(Guid.NewGuid(), projectId, operationId, planJson, createdUtc);
            _records.Add(record);
            return Task.FromResult(record);
        }
    }
}
