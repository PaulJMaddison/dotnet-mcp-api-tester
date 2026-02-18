using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiTester.Ui.Auth;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class OnboardingChecklistTests
{
    private const string ApiKey = "dev-local-key";

    [Fact]
    public async Task ChecklistPost_PersistsPartialSelection_AndKeepsOnboardingIncomplete()
    {
        await using var factory = CreateFactory();
        var client = CreateClient(factory, allowAutoRedirect: false);

        await SignInAsync(client);

        var checklistToken = await GetAntiforgeryTokenAsync(client, "/Onboarding/Checklist");
        var partialPost = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", checklistToken),
            new KeyValuePair<string, string>("ChecklistItems", "project"),
            new KeyValuePair<string, string>("ChecklistItems", "spec")
        });

        var response = await client.PostAsync("/Onboarding/Checklist", partialPost);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Checklist saved. Complete all items to finish onboarding.", content);
        Assert.Contains("value=\"project\" checked", content);
        Assert.Contains("value=\"spec\" checked", content);

        var projectsResponse = await client.GetAsync("/Projects");
        Assert.Equal(HttpStatusCode.Redirect, projectsResponse.StatusCode);
        Assert.StartsWith("/Onboarding", projectsResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChecklistPost_CompletesOnboarding_WhenAllItemsSelected()
    {
        await using var factory = CreateFactory(builder =>
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
        var client = CreateClient(factory, allowAutoRedirect: false);

        await SignInAsync(client);

        var checklistToken = await GetAntiforgeryTokenAsync(client, "/Onboarding/Checklist");
        var completePost = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", checklistToken),
            new KeyValuePair<string, string>("ChecklistItems", "project"),
            new KeyValuePair<string, string>("ChecklistItems", "spec"),
            new KeyValuePair<string, string>("ChecklistItems", "run"),
            new KeyValuePair<string, string>("ChecklistItems", "alerts")
        });

        var response = await client.PostAsync("/Onboarding/Checklist", completePost);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Checklist complete. You can now explore projects and runs.", content);

        var projectsResponse = await client.GetAsync("/Projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var signInToken = await GetAntiforgeryTokenAsync(client, "/Auth/SignIn");
        var signInContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", signInToken),
            new KeyValuePair<string, string>("ApiKey", ApiKey)
        });

        var response = await client.PostAsync("/Auth/SignIn", signInContent);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Onboarding", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(match.Success, "Anti-forgery token was not found in the response.");

        return match.Groups[1].Value;
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

    private static WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configure = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:ApiKey", ApiKey);
            builder.UseSetting("Auth:ApiKeys:0", ApiKey);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Auth:ApiKey"] = ApiKey,
                    ["Auth:ApiKeys:0"] = ApiKey
                };
                config.AddInMemoryCollection(settings);
            });

            configure?.Invoke(builder);
        });

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool allowAutoRedirect)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });
    }
}
