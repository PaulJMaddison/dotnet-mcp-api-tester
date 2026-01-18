using System.Net;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class TestPlanRunnerTests
{
    [Fact]
    public async Task RunPlanAsync_ClassifiesMissingRequiredParamAsExpectedBlock()
    {
        var plan = new TestPlan
        {
            OperationId = "getPet",
            Method = "GET",
            PathTemplate = "/pets/{petId}",
            Cases =
            [
                new TestCase
                {
                    Name = "Missing required path param 'petId'"
                }
            ]
        };

        var runtime = new ApiRuntimeConfig();
        var store = new FakeTestRunStore();
        var runner = new TestPlanRunner(
            new OpenApiStore(),
            runtime,
            new StaticHttpClientFactory(new HttpClient(new StaticResponseHandler())),
            store,
            new SsrfGuard(),
            NullLogger<TestPlanRunner>.Instance);

        var record = await runner.RunPlanAsync(plan, "default", CancellationToken.None);

        Assert.Equal(1, record.Result.Blocked);
        Assert.Equal(1, record.Result.ClassificationSummary.BlockedExpected);
    }

    [Fact]
    public async Task RunPlanAsync_TruncatesResponseSnippets_ByMaxResponseBytes()
    {
        var plan = new TestPlan
        {
            OperationId = "getPet",
            Method = "GET",
            PathTemplate = "/pets/{petId}",
            Cases =
            [
                new TestCase
                {
                    Name = "Happy",
                    PathParams = new Dictionary<string, string> { ["petId"] = "1" },
                    ExpectedStatusCodes = new List<int> { 200 }
                }
            ]
        };

        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:1234");
        runtime.Policy.MaxResponseBodyBytes = 5;

        var handler = new StaticResponseHandler(new string('b', 20));
        var runner = new TestPlanRunner(
            new OpenApiStore(),
            runtime,
            new StaticHttpClientFactory(new HttpClient(handler)),
            new FakeTestRunStore(),
            new SsrfGuard(),
            NullLogger<TestPlanRunner>.Instance);

        var record = await runner.RunPlanAsync(plan, "default", CancellationToken.None);

        var snippet = record.Result.Results.Single().ResponseSnippet;
        Assert.Contains("truncated", snippet, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeTestRunStore : ITestRunStore
    {
        public TestRunRecord? LastRecord { get; private set; }

        public Task SaveAsync(TestRunRecord record)
        {
            LastRecord = record;
            return Task.CompletedTask;
        }

        public Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId)
            => Task.FromResult<TestRunRecord?>(null);

        public Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId)
            => Task.FromResult(false);

        public Task<PagedResult<TestRunRecord>> ListAsync(Guid tenantId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null)
            => Task.FromResult(new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null));

        public Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct)
            => Task.FromResult(0);
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StaticHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StaticResponseHandler(string body = "{}")
        {
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
