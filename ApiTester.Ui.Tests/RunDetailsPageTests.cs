using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.Ui.Auth;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class RunDetailsPageTests
{
    private const string ApiKey = "dev-local-key";

    [Fact]
    public async Task GetRunDetails_ReturnsSummaryHeading()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Run Summary", content);
        Assert.Contains("Case Results", content);
    }

    [Fact]
    public async Task GetRunDetails_AppRoute_ReturnsSummaryHeading()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/app/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Run Summary", content);
    }

    [Fact]
    public async Task GetRunDetails_RendersTopNavigation()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

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
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<th>Name</th>", content);
        Assert.Contains("<th>Status</th>", content);
        Assert.Contains("<th>Status Code</th>", content);
        Assert.Contains("<th>Duration (ms)</th>", content);
        Assert.Contains("<th>Method</th>", content);
        Assert.Contains("<th>URL</th>", content);
    }

    [Fact]
    public async Task GetRunDetails_RendersExpandableDetailSections()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, runId));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<summary>failureReason</summary>", content);
        Assert.Contains("<summary>blockReason</summary>", content);
        Assert.Contains("<summary>responseSnippet</summary>", content);
    }

    [Fact]
    public async Task GetRunDetails_RunNotFound_ShowsMessage()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/runs/{runId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Run not found", content);
    }

    [Fact]
    public async Task GetRunDetails_Unauthorized_RedirectsToSignIn()
    {
        var runId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);

        var response = await client.GetAsync($"/runs/{runId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/Auth/SignIn", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"returnUrl=%2Fruns%2F{runId}", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeHttpMessageHandler handler)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Auth:ApiKey"] = ApiKey
                };
                config.AddInMemoryCollection(settings);
            });

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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);
        return client;
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
