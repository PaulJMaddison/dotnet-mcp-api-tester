using System.Net;
using Microsoft.Extensions.Configuration;

namespace ApiTester.Web.IntegrationTests;

public sealed class BillingConfigurationTests
{
    [Fact]
    public async Task BillingEndpoints_ReturnNotImplemented_WhenStripeNotConfigured()
    {
        using var baseFactory = new ApiTesterWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stripe:SecretKey"] = string.Empty,
                    ["Stripe:WebhookSecret"] = string.Empty,
                    ["Stripe:ProPriceId"] = string.Empty,
                    ["Stripe:TeamPriceId"] = string.Empty
                });
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiTesterWebFactory.ApiKeyAlpha);

        var response = await client.GetAsync("/api/v1/billing/plan");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
