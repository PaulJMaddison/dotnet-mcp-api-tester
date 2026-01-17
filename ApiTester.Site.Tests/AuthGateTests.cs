using ApiTester.Site.Components.Shared;
using ApiTester.Site.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Site.Tests;

public class AuthGateTests
{
    [Fact]
    public void AppAuthGate_RedirectsWhenUnsignedIn()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IApiKeySession>(new FakeApiKeySession(false));
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var cut = context.RenderComponent<AppAuthGate>(parameters => parameters.AddChildContent("<p>Protected</p>"));
        var navigation = context.Services.GetRequiredService<NavigationManager>() as TestNavigationManager;

        Assert.NotNull(navigation);
        Assert.EndsWith("/app/sign-in", navigation!.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Protected", cut.Markup);
    }

    [Fact]
    public void AppAuthGate_RendersContentWhenSignedIn()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IApiKeySession>(new FakeApiKeySession(true));
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var cut = context.RenderComponent<AppAuthGate>(parameters => parameters.AddChildContent("<p>Protected</p>"));

        Assert.Contains("Protected", cut.Markup);
    }

    private sealed class FakeApiKeySession : IApiKeySession
    {
        public FakeApiKeySession(bool hasApiKey)
        {
            HasApiKey = hasApiKey;
        }

        public bool HasApiKey { get; }

        public string? GetApiKey() => HasApiKey ? "test-key" : null;

        public void SetApiKey(string apiKey)
        {
        }

        public void ClearApiKey()
        {
        }
    }
}
