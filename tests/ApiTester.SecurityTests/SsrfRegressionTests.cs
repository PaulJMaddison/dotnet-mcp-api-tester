using System.Net;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using ApiTester.Web.IntegrationTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiTester.SecurityTests;

public sealed class SsrfRegressionTests
{
    [Fact(DisplayName = "SSRF-01 blocks 127.0.0.1 in hosted mode")]
    public async Task Ssrf01_BlocksLoopbackLiteral()
    {
        var result = await ExecuteSingleCaseAsync("http://127.0.0.1:5001", hostedMode: true, blockLocalhost: true, blockPrivateNetworks: true);
        Assert.True(result.Blocked);
        Assert.Contains("Loopback", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SSRF-02 blocks localhost in hosted mode")]
    public async Task Ssrf02_BlocksLocalhostHost()
    {
        var result = await ExecuteSingleCaseAsync("http://localhost:5001", hostedMode: true, blockLocalhost: true, blockPrivateNetworks: true);
        Assert.True(result.Blocked);
        Assert.Contains("localhost", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SSRF-03 blocks RFC1918 range in hosted mode")]
    public async Task Ssrf03_BlocksPrivateRange()
    {
        var result = await ExecuteSingleCaseAsync("http://10.0.0.1", hostedMode: true, blockLocalhost: true, blockPrivateNetworks: true);
        Assert.True(result.Blocked);
        Assert.Contains("Private IPv4", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SSRF-04 blocks link-local metadata range")]
    public async Task Ssrf04_BlocksLinkLocalRange()
    {
        var result = await ExecuteSingleCaseAsync("http://169.254.169.254", hostedMode: true, blockLocalhost: false, blockPrivateNetworks: true);
        Assert.True(result.Blocked);
        Assert.Contains("Link-local", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SSRF-05 allows approved FixtureApi host")]
    public async Task Ssrf05_AllowsFixtureApiHost()
    {
        await using var fixtureApi = await FixtureApiHost.StartAsync();
        var result = await ExecuteSingleCaseAsync(
            fixtureApi.BaseUrl,
            hostedMode: true,
            blockLocalhost: false,
            blockPrivateNetworks: false,
            handler: new StaticResponseHandler("{\"ok\":true}"));

        Assert.False(result.Blocked);
        Assert.True(result.Pass);
    }

    [Fact(DisplayName = "SSRF-06 blocks redirect from allowed host to blocked host")]
    public async Task Ssrf06_BlocksRedirectToBlockedTarget()
    {
        var result = await ExecuteSingleCaseAsync(
            "http://1.1.1.1",
            hostedMode: true,
            blockLocalhost: true,
            blockPrivateNetworks: true,
            handler: new RedirectToBlockedHostHandler());

        Assert.True(result.Blocked);
        Assert.True(
            (result.BlockReason ?? string.Empty).Contains("Redirect blocked by policy", StringComparison.OrdinalIgnoreCase)
            || (result.BlockReason ?? string.Empty).Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "SSRF-07 redirect loops are capped")]
    public async Task Ssrf07_CapsRedirectLoops()
    {
        var result = await ExecuteSingleCaseAsync(
            "https://safe.example",
            hostedMode: true,
            blockLocalhost: false,
            blockPrivateNetworks: false,
            handler: new RedirectLoopHandler());

        Assert.True(result.Blocked);
        Assert.Contains("Redirect", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SSRF-08 rejects URL normalization/userinfo tricks")]
    public async Task Ssrf08_RejectsUserInfoNormalizationTrick()
    {
        var result = await ExecuteSingleCaseAsync(
            "http://allowed.example@127.0.0.1",
            hostedMode: true,
            blockLocalhost: true,
            blockPrivateNetworks: true);

        Assert.True(result.Blocked);
        Assert.Contains("Loopback", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TestCaseResult> ExecuteSingleCaseAsync(
        string baseUrl,
        bool hostedMode,
        bool blockLocalhost,
        bool blockPrivateNetworks,
        string path = "/fixture/status",
        HttpMessageHandler? handler = null)
    {
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl(baseUrl);
        runtime.Policy.DryRun = false;
        runtime.Policy.HostedMode = hostedMode;
        runtime.Policy.BlockLocalhost = blockLocalhost;
        runtime.Policy.BlockPrivateNetworks = blockPrivateNetworks;
        runtime.Policy.AllowedMethods.Clear();
        runtime.Policy.AllowedMethods.Add("GET");
        runtime.Policy.AllowedBaseUrls.Clear();
        runtime.Policy.AllowedBaseUrls.Add(baseUrl.TrimEnd('/'));

        var plan = new TestPlan
        {
            OperationId = "securityCheck",
            Method = "GET",
            PathTemplate = path,
            Cases =
            [
                new TestCase
                {
                    Name = "security",
                    ExpectedStatusCodes = [200]
                }
            ]
        };

        var runner = new TestPlanRunner(
            new OpenApiStore(),
            runtime,
            new StaticHttpClientFactory(CreateClient(handler)),
            new InMemoryRunStore(),
            new SsrfGuard(),
            NullLogger<TestPlanRunner>.Instance);

        var run = await runner.RunPlanAsync(plan, "security", CancellationToken.None);
        return run.Result.Results.Single();
    }


    private static HttpClient CreateClient(HttpMessageHandler? handler)
    {
        if (handler is null)
        {
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        }

        return new HttpClient(handler);
    }

    private sealed class InMemoryRunStore : ITestRunStore
    {
        public Task SaveAsync(TestRunRecord record) => Task.CompletedTask;
        public Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId) => Task.FromResult<TestRunRecord?>(null);
        public Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId) => Task.FromResult(false);
        public Task<PagedResult<TestRunRecord>> ListAsync(Guid tenantId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null, DateTimeOffset? notBeforeUtc = null)
            => Task.FromResult(new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null));
        public Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult(0);
        public Task<int> TrimResponseSnippetsAsync(Guid tenantId, int maxSnippetLength, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StaticHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StaticResponseHandler(string body = "{}") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }


    private sealed class RedirectToBlockedHostHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data");
            return Task.FromResult(response);
        }
    }

    private sealed class RedirectLoopHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = request.RequestUri;
            return Task.FromResult(response);
        }
    }

    private sealed class RedirectorHost : IAsyncDisposable
    {
        private readonly IHost _host;

        private RedirectorHost(IHost host, string baseUrl)
        {
            _host = host;
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public string BaseUrl { get; }

        public static async Task<RedirectorHost> StartAsync(CancellationToken ct = default)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();
            app.MapGet("/redirect-to", (string url) => Results.Redirect(url));
            await app.StartAsync(ct);

            _ = app.Services.GetRequiredService<IServer>();
            var baseUrl = app.Urls.First();
            return new RedirectorHost(app, baseUrl);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
