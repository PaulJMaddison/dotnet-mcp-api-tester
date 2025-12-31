using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class ProjectsPageTests
{
    [Fact]
    public async Task GetRoot_ReturnsProjectsHeading()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Projects", content);
    }

    [Fact]
    public async Task GetRoot_WithEmptyProjects_ShowsEmptyState()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApiTesterWebClient>();

                var payload = new
                {
                    projects = Array.Empty<object>(),
                    metadata = new { total = 0, pageSize = 50, nextPageToken = (string?)null }
                };
                var json = JsonSerializer.Serialize(payload);
                var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("http://localhost")
                };

                services.AddSingleton(new ApiTesterWebClient(httpClient));
            });
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No projects yet, create one by running the client or API.", content);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
