using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiTester.Ui.Auth;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Ui.Tests;

public class PolicyPageTests
{
    private const string ApiKey = "dev-local-key";

    [Fact]
    public async Task GetPolicyPage_RendersPolicyEditorAndGuidance()
    {
        var handler = new FakeHttpMessageHandler(BuildResponse);

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var response = await client.GetAsync("/app/policy");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Policy editor", content);
        Assert.Contains("SSRF guidance", content);
        Assert.Contains("Allowlists (JSON array)", content);
    }

    [Fact]
    public async Task GetPolicyPage_Unauthorized_RedirectsToSignIn()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var factory = CreateFactory(handler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);

        var response = await client.GetAsync("/app/policy");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/Auth/SignIn", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnUrl=%2Fapp%2Fpolicy", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPolicyPage_InvalidJson_ShowsValidationErrors()
    {
        var handler = new FakeHttpMessageHandler(BuildResponse);

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var token = await GetAntiforgeryTokenAsync(client, "/app/policy");
        var postContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("AllowedBaseUrlsJson", "not-json"),
            new KeyValuePair<string, string>("AllowedMethodsJson", "[\"GET\"]"),
            new KeyValuePair<string, string>("TimeoutSecondsInput", "30"),
            new KeyValuePair<string, string>("MaxRequestBodyBytesInput", "1024"),
            new KeyValuePair<string, string>("MaxResponseBodyBytesInput", "2048")
        });

        var response = await client.PostAsync("/app/policy", postContent);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fix the following before saving", content);
        Assert.Contains("Allowlists (JSON array) is invalid JSON", content);
        Assert.Equal(0, handler.PutCount);
    }

    [Fact]
    public async Task PostPolicyPage_ValidInput_UpdatesPolicy()
    {
        var handler = new FakeHttpMessageHandler(BuildResponse);

        await using var factory = CreateFactory(handler);
        var client = CreateClient(factory);

        var token = await GetAntiforgeryTokenAsync(client, "/app/policy");
        var postContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("AllowedBaseUrlsJson", "[\"https://api.example.com\"]"),
            new KeyValuePair<string, string>("AllowedMethodsJson", "[\"GET\",\"POST\"]"),
            new KeyValuePair<string, string>("TimeoutSecondsInput", "45"),
            new KeyValuePair<string, string>("MaxRequestBodyBytesInput", "4096"),
            new KeyValuePair<string, string>("MaxResponseBodyBytesInput", "8192")
        });

        var response = await client.PostAsync("/app/policy", postContent);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Policy saved.", content);
        Assert.Equal(1, handler.PutCount);
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
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);
        return client;
    }

    private static HttpResponseMessage BuildResponse(HttpRequestMessage request)
    {
        if (request.RequestUri?.AbsolutePath == "/api/runtime/policy" && request.Method == HttpMethod.Get)
        {
            return Json(new
            {
                dryRun = false,
                allowedBaseUrls = new[] { "https://api.example.com" },
                allowedMethods = new[] { "GET" },
                timeoutSeconds = 30,
                maxRequestBodyBytes = 1024,
                maxResponseBodyBytes = 2048,
                validateSchema = true,
                blockLocalhost = true,
                blockPrivateNetworks = true,
                retryOnFlake = false,
                maxRetries = 0
            });
        }

        if (request.RequestUri?.AbsolutePath == "/api/runtime/policy" && request.Method == HttpMethod.Put)
        {
            return Json(new
            {
                dryRun = false,
                allowedBaseUrls = new[] { "https://api.example.com" },
                allowedMethods = new[] { "GET", "POST" },
                timeoutSeconds = 45,
                maxRequestBodyBytes = 4096,
                maxResponseBodyBytes = 8192,
                validateSchema = true,
                blockLocalhost = true,
                blockPrivateNetworks = true,
                retryOnFlake = false,
                maxRetries = 0
            });
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

        public int PutCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                Requests.Add(request.RequestUri);
            }

            if (request.Method == HttpMethod.Put && request.RequestUri?.AbsolutePath == "/api/runtime/policy")
            {
                PutCount++;
            }

            return Task.FromResult(_handler(request));
        }
    }
}
