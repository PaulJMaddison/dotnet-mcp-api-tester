using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ApiTester.Site.Tests;

public sealed class DevAuthBypassTests
{
    [Fact]
    public async Task Development_WithDevBypassEnabled_ReturnsUnauthorizedWithoutSession()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Auth:Authority", string.Empty);
            builder.UseSetting("Auth:ClientId", string.Empty);
            builder.UseSetting("Auth:ClientSecret", string.Empty);
            builder.UseSetting("Auth:DevBypass", "true");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Authority"] = string.Empty,
                    ["Auth:ClientId"] = string.Empty,
                    ["Auth:ClientSecret"] = string.Empty,
                    ["Auth:DevBypass"] = "true"
                });
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/signin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/app", response.Headers.Location?.OriginalString);
    }
}
