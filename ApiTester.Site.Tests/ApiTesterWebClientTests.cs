using System.Net;
using System.Text;
using ApiTester.Site.Services;

namespace ApiTester.Site.Tests;

public class ApiTesterWebClientTests
{
    [Fact]
    public async Task GetRunsAsync_ComposesQueryWithFilters()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new ApiTesterWebClient(httpClient);

        await client.GetRunsAsync("project-one", "op-1", 5, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("/api/runs?projectKey=project-one&operationId=op-1&take=5", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetRunsAsync_ComposesQueryWithProjectOnly()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new ApiTesterWebClient(httpClient);

        await client.GetRunsAsync("project-two", null, null, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("/api/runs?projectKey=project-two", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"projectKey\":\"demo\",\"runs\":[],\"metadata\":{\"total\":0,\"pageSize\":20,\"nextPageToken\":null}}",
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
