using ApiTester.Web.Auth;
using ApiTester.Web.Observability;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.UnitTests;

public sealed class ApiKeyRedactionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RedactsApiKeyAndAuthorizationHeaders()
    {
        var middleware = new ApiKeyRedactionMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[ApiKeyAuthDefaults.HeaderName] = "alpha01.secret";
        context.Request.Headers[ApiKeyAuthDefaults.AuthorizationHeaderName] = "Bearer super-secret-token";

        await middleware.InvokeAsync(context);

        Assert.Equal(ApiKeyAuthDefaults.RedactedValue, context.Request.Headers[ApiKeyAuthDefaults.HeaderName].ToString());
        Assert.Equal(ApiKeyAuthDefaults.RedactedValue, context.Request.Headers[ApiKeyAuthDefaults.AuthorizationHeaderName].ToString());
        Assert.Equal("alpha01.secret", context.Items[ApiKeyAuthDefaults.RawApiKeyItemName]);
        Assert.Equal("Bearer super-secret-token", context.Items[ApiKeyAuthDefaults.RawAuthorizationItemName]);
    }
}
