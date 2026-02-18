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

public sealed class AiAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_RedactsInputsAndOutputs()
    {
        var provider = new CapturingAiProvider(_ =>
        {
            var payload = new
            {
                insights = new[]
                {
                    new
                    {
                        type = "summary",
                        payload = new { text = "found secret token" }
                    }
                }
            };
            var json = JsonSerializer.Serialize(payload, JsonDefaults.Default);
            return new AiResult(json, "test-model");
        });

        var redactionService = new RedactionService();
        var insightStore = new InMemoryAiInsightStore();
        var rateLimiter = new AiRateLimiter(
            Options.Create(new AiRateLimitOptions { Capacity = 10, RefillTokensPerMinute = 10 }),
            TimeProvider.System);

        var service = new AiAnalysisService(provider, redactionService, insightStore, rateLimiter, TimeProvider.System);
        var org = BuildOrg(["secret"]);
        var input = BuildInput(org, "secret in response");

        var results = await service.AnalyzeAsync(input, CancellationToken.None);

        Assert.Single(provider.Requests);
        Assert.DoesNotContain("secret", provider.Requests[0], StringComparison.OrdinalIgnoreCase);
        Assert.Single(results);
        Assert.DoesNotContain("secret", results[0].JsonPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", results[0].JsonPayload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_EnforcesRateLimits()
    {
        var provider = new CapturingAiProvider(_ => new AiResult("{\"insights\":[]}", "test-model"));
        var redactionService = new RedactionService();
        var insightStore = new InMemoryAiInsightStore();
        var fixedTime = new FixedTimeProvider(new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var rateLimiter = new AiRateLimiter(
            Options.Create(new AiRateLimitOptions { Capacity = 1, RefillTokensPerMinute = 1 }),
            fixedTime);

        var service = new AiAnalysisService(provider, redactionService, insightStore, rateLimiter, fixedTime);
        var org = BuildOrg();
        var input = BuildInput(org, "ok");

        await service.AnalyzeAsync(input, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<AiRateLimitExceededException>(() => service.AnalyzeAsync(input, CancellationToken.None));
        Assert.Contains("rate limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidatesSchema()
    {
        var provider = new CapturingAiProvider(_ => new AiResult("{\"insights\":[{\"type\":\"summary\"}]}", "test-model"));
        var redactionService = new RedactionService();
        var insightStore = new InMemoryAiInsightStore();
        var rateLimiter = new AiRateLimiter(
            Options.Create(new AiRateLimitOptions { Capacity = 10, RefillTokensPerMinute = 10 }),
            TimeProvider.System);

        var service = new AiAnalysisService(provider, redactionService, insightStore, rateLimiter, TimeProvider.System);
        var org = BuildOrg();
        var input = BuildInput(org, "ok");

        await Assert.ThrowsAsync<AiSchemaValidationException>(() => service.AnalyzeAsync(input, CancellationToken.None));
    }

    private static OrganisationRecord BuildOrg(IReadOnlyList<string>? redactionRules = null)
        => new(
            Guid.NewGuid(),
            "Org",
            "org",
            DateTime.UtcNow,
            null,
            redactionRules?.ToList(),
            new OrgSettings(OrgPlan.Pro));

    private static AiAnalysisInput BuildInput(OrganisationRecord org, string responseSnippet)
    {
        var run = new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OrganisationId = org.OrganisationId,
            OperationId = "get:thing",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "get:thing",
                TotalCases = 1,
                Passed = 1,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 5,
                Results = new List<TestCaseResult>
                {
                    new()
                    {
                        Name = "Case",
                        Pass = true,
                        StatusCode = 200,
                        ResponseSnippet = responseSnippet
                    }
                }
            }
        };

        var operation = new OpenApiOperation
        {
            Summary = "Get thing",
            Description = "Returns thing"
        };

        return new AiAnalysisInput(
            org,
            Guid.NewGuid(),
            "get:thing",
            "GET",
            "/thing",
            operation,
            new ApiExecutionPolicySnapshot(false, false, Array.Empty<string>(), Array.Empty<string>(), false, false, 30, 1024, 2048, true, false, 0),
            run);
    }

    private sealed class CapturingAiProvider : IAiProvider
    {
        private readonly Func<string, AiResult> _responder;

        public CapturingAiProvider(Func<string, AiResult> responder)
        {
            _responder = responder;
        }

        public List<string> Requests { get; } = new();

        public Task<AiResult> ExplainApiAsync(string spec, string operationId, CancellationToken ct)
            => Task.FromResult(_responder(spec));

        public Task<AiResult> SuggestEdgeCasesAsync(string spec, string operationId, CancellationToken ct)
            => Task.FromResult(_responder(spec));

        public Task<AiResult> SummariseRunAsync(string runId, string runContext, CancellationToken ct)
            => Task.FromResult(_responder(runContext));

        public Task<AiResult> SuggestFixesAsync(string runId, string runContext, CancellationToken ct)
        {
            Requests.Add(runContext);
            return Task.FromResult(_responder(runContext));
        }
    }

    private sealed class InMemoryAiInsightStore : IAiInsightStore
    {
        private readonly List<AiInsightRecord> _records = new();

        public Task<IReadOnlyList<AiInsightRecord>> ListAsync(Guid organisationId, Guid projectId, Guid runId, CancellationToken ct)
        {
            var list = _records
                .Where(i => i.OrganisationId == organisationId && i.ProjectId == projectId && i.RunId == runId)
                .OrderBy(i => i.CreatedUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<AiInsightRecord>>(list);
        }

        public Task<IReadOnlyList<AiInsightRecord>> CreateAsync(
            Guid organisationId,
            Guid projectId,
            Guid runId,
            string operationId,
            IReadOnlyList<AiInsightCreate> insights,
            string modelId,
            DateTime createdUtc,
            CancellationToken ct)
        {
            var records = insights
                .Select(insight => new AiInsightRecord(
                    Guid.NewGuid(),
                    organisationId,
                    projectId,
                    runId,
                    operationId,
                    insight.Type,
                    insight.JsonPayload,
                    modelId,
                    createdUtc))
                .ToList();

            _records.AddRange(records);
            return Task.FromResult<IReadOnlyList<AiInsightRecord>>(records);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
