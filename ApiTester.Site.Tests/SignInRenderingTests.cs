using ApiTester.Site.Components.Pages.App;
using ApiTester.Site.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Site.Tests;

public class SignInRenderingTests
{
    [Fact]
    public void SignIn_DoesNotRenderApiKeyInMarkup()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IApiKeySession>(new FakeApiKeySession());

        var cut = context.RenderComponent<SignIn>();

        cut.Find("#api-key").Change("super-secret-key");
        cut.Find("form").Submit();

        Assert.DoesNotContain("super-secret-key", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeApiKeySession : IApiKeySession
    {
        public bool HasApiKey => false;

        public string? GetApiKey() => null;

        public void SetApiKey(string apiKey)
        {
        }

        public void ClearApiKey()
        {
        }
    }
}
