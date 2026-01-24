using System.Net;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class TestPlanRunnerRetryTests
{
    [Fact]
    public async Task RunPlanAsync_RetriesFlakyResponses_UpToLimit()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.OK));
        var runtime = BuildRuntimeConfig(retryOnFlake: true, maxRetries: 1);
        var runner = BuildRunner(runtime, handler);

        var plan = BuildPlan();

        var record = await runner.RunPlanAsync(plan, "project-key", CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.True(record.Result.Results[0].Pass);
        Assert.True(record.Result.Results[0].IsFlaky);
        Assert.Equal(ResultClassification.FlakyExternal, record.Result.Results[0].Classification);
    }

    [Fact]
    public async Task RunPlanAsync_DoesNotExceedRetryLimit()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.OK));
        var runtime = BuildRuntimeConfig(retryOnFlake: true, maxRetries: 1);
        var runner = BuildRunner(runtime, handler);

        var plan = BuildPlan();

        var record = await runner.RunPlanAsync(plan, "project-key", CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.False(record.Result.Results[0].Pass);
        Assert.Equal(502, record.Result.Results[0].StatusCode);
    }

    private static TestPlanRunner BuildRunner(ApiRuntimeConfig runtimeConfig, HttpMessageHandler handler)
    {
        var clientFactory = new FakeHttpClientFactory(new HttpClient(handler));
        return new TestPlanRunner(
            new OpenApiStore(),
            runtimeConfig,
            clientFactory,
            new NoopRunStore(),
            new SsrfGuard(),
            NullLogger<TestPlanRunner>.Instance);
    }

    private static ApiRuntimeConfig BuildRuntimeConfig(bool retryOnFlake, int maxRetries)
    {
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("https://example.test");
        runtime.Policy.DryRun = false;
        runtime.Policy.BlockLocalhost = false;
        runtime.Policy.BlockPrivateNetworks = false;
        runtime.Policy.AllowedBaseUrls.Add("https://example.test");
        runtime.Policy.AllowedMethods.Add("GET");
        runtime.Policy.RetryOnFlake = retryOnFlake;
        runtime.Policy.MaxRetries = maxRetries;
        return runtime;
    }

    private static TestPlan BuildPlan()
        => new()
        {
            OperationId = "op",
            Method = "GET",
            PathTemplate = "/widgets",
            Cases =
            {
                new TestCase
                {
                    Name = "case-1",
                    ExpectedStatusCodes = new List<int> { 200 }
                }
            }
        };

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name = "") => _client;
    }

    private sealed class NoopRunStore : ITestRunStore
    {
        public Task SaveAsync(TestRunRecord record) => Task.CompletedTask;
        public Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId) => Task.FromResult<TestRunRecord?>(null);
        public Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId) => Task.FromResult(false);
        public Task<PagedResult<TestRunRecord>> ListAsync(Guid tenantId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null, DateTimeOffset? notBeforeUtc = null)
            => Task.FromResult(new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null));
        public Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = _responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(response);
        }
    }
}
