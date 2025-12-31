using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class RunDetailsPageTests
{
    [Fact]
    public async Task GetRunDetails_ReturnsSummaryHeading()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Run Summary", content);
        Assert.Contains("Case Results", content);
    }

    [Fact]
    public async Task GetRunDetails_RendersTopNavigation()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<a href=\"/\">Projects</a>", content);
    }

    [Fact]
    public async Task GetRunDetails_RendersCaseResultsTableHeaders()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<th>Name</th>", content);
        Assert.Contains("<th>Pass</th>", content);
        Assert.Contains("<th>Blocked</th>", content);
        Assert.Contains("<th>StatusCode</th>", content);
        Assert.Contains("<th>DurationMs</th>", content);
        Assert.Contains("<th>Method</th>", content);
        Assert.Contains("<th>Url</th>", content);
    }

    [Fact]
    public async Task GetRunDetails_RunNotFound_ShowsMessage()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Run not found", content);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeHttpMessageHandler handler)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApiTesterWebClient>();

                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("http://localhost")
                };

                services.AddSingleton(new ApiTesterWebClient(httpClient));
            });
        });
    }

    private static HttpResponseMessage BuildResponse(HttpRequestMessage request, Guid runId)
    {
        if (request.RequestUri?.AbsolutePath == $"/api/runs/{runId}")
        {
            var payload = new
            {
                runId,
                projectKey = "sample-project",
                operationId = "op-777",
                startedUtc = DateTimeOffset.UtcNow,
                completedUtc = DateTimeOffset.UtcNow,
                result = new
                {
                    operationId = "op-777",
                    totalCases = 2,
                    passed = 1,
                    failed = 1,
                    blocked = 0,
                    totalDurationMs = 120,
                    results = new[]
                    {
                        new
                        {
                            name = "Fetch sample",
                            blocked = false,
                            blockReason = (string?)null,
                            url = "https://example.test",
                            method = "GET",
                            statusCode = 500,
                            durationMs = 42,
                            pass = false,
                            failureReason = "Unexpected status code",
                            responseSnippet = "{\"error\":\"boom\"}"
                        }
                    }
                }
            };

            return Json(payload);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
