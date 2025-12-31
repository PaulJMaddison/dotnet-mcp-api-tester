using System.Linq;
using ApiTester.Web.Observability;

namespace ApiTester.Web.IntegrationTests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task AddsCorrelationIdHeader()
    {
        using var factory = new ApiTesterWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues(CorrelationIdDefaults.HeaderName, out var values));
        var correlationId = values.Single();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }
}
