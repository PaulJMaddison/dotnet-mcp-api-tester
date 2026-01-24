using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class TestPlanRunnerSchemaValidationTests
{
    [Fact]
    public async Task RunPlanAsync_FailsOnContentTypeMismatch()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(BuildSchemaWithRequiredName()));

        var runtime = BuildRuntimeConfig();
        var handler = new StaticResponseHandler("{\"name\":\"ok\"}", "text/plain");

        var runner = BuildRunner(store, runtime, handler);
        var record = await runner.RunPlanAsync(BuildPlan(), "default", CancellationToken.None);

        using var failure = ParseFailure(record.Result.Results.Single().FailureReason);
        Assert.Equal("content_type_mismatch", failure.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task RunPlanAsync_FailsWhenRequiredPropertyMissing()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(BuildSchemaWithRequiredName()));

        var runtime = BuildRuntimeConfig();
        var handler = new StaticResponseHandler("{}", "application/json");

        var runner = BuildRunner(store, runtime, handler);
        var record = await runner.RunPlanAsync(BuildPlan(), "default", CancellationToken.None);

        using var failure = ParseFailure(record.Result.Results.Single().FailureReason);
        Assert.Equal("schema_mismatch", failure.RootElement.GetProperty("type").GetString());
        Assert.Contains("missing required property", failure.RootElement.GetProperty("details").GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task RunPlanAsync_FailsWhenTypeIsWrong()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(BuildSchemaWithIntegerAge()));

        var runtime = BuildRuntimeConfig();
        var handler = new StaticResponseHandler("{\"age\":\"oops\"}", "application/json");

        var runner = BuildRunner(store, runtime, handler);
        var record = await runner.RunPlanAsync(BuildPlan(), "default", CancellationToken.None);

        using var failure = ParseFailure(record.Result.Results.Single().FailureReason);
        Assert.Equal("schema_mismatch", failure.RootElement.GetProperty("type").GetString());
        Assert.Contains("expected integer", failure.RootElement.GetProperty("details").GetProperty("errors")[0].GetString());
    }

    private static JsonDocument ParseFailure(string? failureReason)
    {
        Assert.False(string.IsNullOrWhiteSpace(failureReason));
        return JsonDocument.Parse(failureReason!);
    }

    private static TestPlanRunner BuildRunner(OpenApiStore store, ApiRuntimeConfig runtime, HttpMessageHandler handler)
    {
        return new TestPlanRunner(
            store,
            runtime,
            new StaticHttpClientFactory(new HttpClient(handler)),
            new FakeTestRunStore(),
            new SsrfGuard(),
            NullLogger<TestPlanRunner>.Instance);
    }

    private static ApiRuntimeConfig BuildRuntimeConfig()
    {
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:1234");
        runtime.Policy.ValidateSchema = true;
        return runtime;
    }

    private static TestPlan BuildPlan()
    {
        return new TestPlan
        {
            OperationId = "getPet",
            Method = "GET",
            PathTemplate = "/pets/{petId}",
            Cases =
            [
                new TestCase
                {
                    Name = "case-1",
                    PathParams = new Dictionary<string, string> { ["petId"] = "1" },
                    ExpectedStatusCodes = new List<int> { 200 }
                }
            ]
        };
    }

    private static OpenApiDocument BuildDocument(OpenApiSchema schema)
    {
        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/pets/{petId}"] = new OpenApiPathItem
                {
                    Operations =
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            OperationId = "getPet",
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static OpenApiSchema BuildSchemaWithRequiredName()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "name" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["name"] = new() { Type = "string" }
            }
        };
    }

    private static OpenApiSchema BuildSchemaWithIntegerAge()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "age" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["age"] = new() { Type = "integer" }
            }
        };
    }

    private sealed class FakeTestRunStore : ITestRunStore
    {
        public Task SaveAsync(TestRunRecord record) => Task.CompletedTask;
        public Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId) => Task.FromResult<TestRunRecord?>(null);
        public Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId) => Task.FromResult(false);
        public Task<PagedResult<TestRunRecord>> ListAsync(Guid tenantId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null, DateTimeOffset? notBeforeUtc = null)
            => Task.FromResult(new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null));
        public Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult(0);
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
        private readonly string _contentType;

        public StaticResponseHandler(string body, string contentType)
        {
            _body = body;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, _contentType)
            };

            return Task.FromResult(response);
        }
    }
}
