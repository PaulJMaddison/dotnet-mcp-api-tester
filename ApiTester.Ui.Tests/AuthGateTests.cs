using System.Net;
using ApiTester.Ui.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ApiTester.Ui.Tests;

public class AuthGateTests
{
    private const string ApiKey = "test-auth-key";

    [Fact]
    public async Task GetRuns_RedirectsToSignIn_WhenMissingApiKey()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Runs");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/Auth/SignIn", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnUrl=%2FRuns", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRuns_AllowsAccess_WhenAuthenticatedFromHeader()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, ApiKey);

        var response = await client.GetAsync("/Runs");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Runs", content);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
        });
    }
}
