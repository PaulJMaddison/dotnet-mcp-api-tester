using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ApiTester.Site.Tests;

public sealed class DevAuthBypassTests
{
    [Fact]
    public async Task Development_WithDevBypassEnabled_AllowsAppRouteWithoutAuth()
    {
        using var baseFactory = new SiteWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
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

        var response = await client.GetAsync("/app/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
