using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class ExecuteToolsTests
{
    [Fact]
    public async Task ApiCallOperation_BlocksDisallowedMethod()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(OperationType.Post, "createPet", "http://127.0.0.1:1234"));

        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:1234");
        runtime.Policy.DryRun = false;
        runtime.Policy.AllowedMethods.Clear();
        runtime.Policy.AllowedMethods.Add("GET");

        var handler = new StaticResponseHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(handler);
        var factory = new StaticHttpClientFactory(client);

        var tools = new ExecuteTools(store, runtime, factory, new SsrfGuard(), NullLogger<ExecuteTools>.Instance);

        var response = await tools.ApiCallOperation("createPet", ct: CancellationToken.None);

        using var doc = JsonDocument.Parse(response);
        Assert.True(doc.RootElement.GetProperty("blocked").GetBoolean());
        Assert.Contains("Method not allowed", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ApiCallOperation_BlocksBaseUrlNotInAllowList()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(OperationType.Get, "listPets", "http://127.0.0.1:1234"));

        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:1234");
        runtime.Policy.DryRun = false;
        runtime.Policy.AllowedMethods.Clear();
        runtime.Policy.AllowedMethods.Add("GET");
        runtime.Policy.AllowedBaseUrls.Add("https://allowed.example.com");

        var handler = new StaticResponseHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(handler);
        var factory = new StaticHttpClientFactory(client);

        var tools = new ExecuteTools(store, runtime, factory, new SsrfGuard(), NullLogger<ExecuteTools>.Instance);

        var response = await tools.ApiCallOperation("listPets", ct: CancellationToken.None);

        using var doc = JsonDocument.Parse(response);
        Assert.True(doc.RootElement.GetProperty("blocked").GetBoolean());
        Assert.Contains("Base URL not allowed", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ApiCallOperation_AppliesTimeoutAndResponseSizeLimits()
    {
        var store = new OpenApiStore();
        store.SetDocument(BuildDocument(OperationType.Get, "listPets", "http://127.0.0.1:1234"));

        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:1234");
        runtime.Policy.DryRun = false;
        runtime.Policy.BlockLocalhost = false;
        runtime.Policy.BlockPrivateNetworks = false;
        runtime.Policy.AllowedMethods.Clear();
        runtime.Policy.AllowedMethods.Add("GET");
        runtime.Policy.AllowedBaseUrls.Add("http://127.0.0.1:1234");
        runtime.Policy.Timeout = TimeSpan.FromSeconds(5);
        runtime.Policy.MaxResponseBodyBytes = 10;

        var body = new string('a', 100);
        var handler = new StaticResponseHandler(HttpStatusCode.OK, body);
        var client = new HttpClient(handler);
        var factory = new StaticHttpClientFactory(client);

        var tools = new ExecuteTools(store, runtime, factory, new SsrfGuard(), NullLogger<ExecuteTools>.Instance);

        var response = await tools.ApiCallOperation("listPets", ct: CancellationToken.None);

        Assert.Equal(runtime.Policy.Timeout, client.Timeout);

        using var doc = JsonDocument.Parse(response);
        var responseBody = doc.RootElement.GetProperty("body").GetString();
        Assert.Contains("truncated", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static OpenApiDocument BuildDocument(OperationType method, string operationId, string baseUrl)
    {
        return new OpenApiDocument
        {
            Servers = new List<OpenApiServer> { new() { Url = baseUrl } },
            Paths = new OpenApiPaths
            {
                ["/pets"] = new OpenApiPathItem
                {
                    Operations =
                    {
                        [method] = new OpenApiOperation
                        {
                            OperationId = operationId,
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse()
                            }
                        }
                    }
                }
            }
        };
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
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StaticResponseHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
