using ApiTester.Site.Components.Pages.App;
using ApiTester.Site.Services;
using Bunit;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Site.Tests;

public class OnboardingChecklistTests
{
    [Fact]
    public void Onboarding_RendersChecklistItems()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IApiKeySession>(new FakeApiKeySession());
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var cut = context.RenderComponent<Onboarding>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Onboarding checklist", cut.Markup);
            Assert.Contains("Create project", cut.Markup);
            Assert.Contains("Import OpenAPI", cut.Markup);
            Assert.Contains("Set base URL + policy", cut.Markup);
            Assert.Contains("Run test plan", cut.Markup);
        });
    }

    private sealed class FakeApiKeySession : IApiKeySession
    {
        public bool HasApiKey => true;

        public string? GetApiKey() => "test-key";

        public void SetApiKey(string apiKey)
        {
        }

        public void ClearApiKey()
        {
        }
    }
}
