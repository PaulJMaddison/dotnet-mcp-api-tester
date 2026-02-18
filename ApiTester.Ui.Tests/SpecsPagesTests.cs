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

public class SpecsPagesTests
{
    private const string ApiKey = "dev-local-key";

    [Fact]
    public async Task GetSpecsPage_RendersImportedSpecs()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(request => BuildResponse(request, projectId, specId));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/app/specs?projectId={projectId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("OpenAPI Specs", content);
        Assert.Contains(specId.ToString(), content);
        Assert.Contains("Import spec", content);
    }

    [Fact]
    public async Task GetOperationDescribe_WhenApiUnauthorized_RedirectsToSignIn()
    {
        var projectId = Guid.NewGuid();
        var operationId = "orders_get";
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory, followRedirect: false);

        var response = await client.GetAsync($"/app/operations/{operationId}?projectId={projectId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Auth/SignIn", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeHttpMessageHandler handler)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:ApiKey", ApiKey);
            builder.UseSetting("Auth:ApiKeys:0", ApiKey);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:ApiKey"] = ApiKey,
                    ["Auth:ApiKeys:0"] = ApiKey
                });
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool followRedirect = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = followRedirect
        });

        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);
        return client;
    }

    private static HttpResponseMessage BuildResponse(HttpRequestMessage request, Guid projectId, Guid specId)
    {
        var path = request.RequestUri?.AbsolutePath;
        if (path == "/api/projects")
        {
            var payload = new
            {
                projects = new[]
                {
                    new
                    {
                        projectId,
                        name = "Sample Project",
                        projectKey = "sample-project",
                        createdUtc = DateTime.UtcNow
                    }
                },
                metadata = new { total = 1, pageSize = 100, nextPageToken = (string?)null }
            };

            return Json(payload);
        }

        if (path == $"/api/projects/{projectId}/specs")
        {
            var payload = new[]
            {
                new
                {
                    specId,
                    projectId,
                    title = "Sample Spec",
                    version = "1.0.0",
                    specHash = "abc123",
                    uploadedUtc = DateTime.UtcNow
                }
            };

            return Json(payload);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
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
