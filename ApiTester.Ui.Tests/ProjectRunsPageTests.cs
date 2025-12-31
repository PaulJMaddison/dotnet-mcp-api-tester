using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class ProjectRunsPageTests
{
    [Fact]
    public async Task GetProjectRuns_ReturnsRunsHeading()
    {
        var projectId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, projectId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/projects/{projectId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Runs", content);
    }

    [Fact]
    public async Task GetProjectRuns_RendersTopNavigation()
    {
        var projectId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, projectId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/projects/{projectId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<a href=\"/\">Projects</a>", content);
    }

    [Fact]
    public async Task GetProjectRuns_WithOperationIdFilter_UsesOperationIdQuery()
    {
        var projectId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, projectId));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/projects/{projectId}?operationId=op-999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runRequest = handler.Requests.SingleOrDefault(uri => uri.AbsolutePath == "/api/runs");
        Assert.NotNull(runRequest);
        Assert.Contains("operationId=op-999", runRequest!.Query);
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

    private static HttpResponseMessage BuildResponse(HttpRequestMessage request, Guid projectId)
    {
        if (request.RequestUri?.AbsolutePath == $"/api/projects/{projectId}")
        {
            var payload = new
            {
                projectId,
                name = "Sample Project",
                projectKey = "sample-project",
                createdUtc = DateTime.UtcNow
            };

            return Json(payload);
        }

        if (request.RequestUri?.AbsolutePath == "/api/runs")
        {
            var payload = new
            {
                projectKey = "sample-project",
                runs = new[]
                {
                    new
                    {
                        runId = Guid.NewGuid(),
                        projectKey = "sample-project",
                        operationId = "op-999",
                        startedUtc = DateTimeOffset.UtcNow,
                        completedUtc = DateTimeOffset.UtcNow,
                        snapshot = new
                        {
                            totalCases = 12,
                            passed = 10,
                            failed = 1,
                            blocked = 1,
                            totalDurationMs = 450
                        }
                    }
                },
                metadata = new
                {
                    total = 1,
                    pageSize = 20,
                    nextPageToken = (string?)null
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

        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                Requests.Add(request.RequestUri);
            }

            return Task.FromResult(_handler(request));
        }
    }
}
