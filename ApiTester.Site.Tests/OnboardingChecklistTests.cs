using ApiTester.Site.Components.Pages.App;
using Bunit;

namespace ApiTester.Site.Tests;

public class OnboardingChecklistTests
{
    [Fact]
    public void Onboarding_RendersChecklistItems()
    {
        using var context = new TestContext();
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
}
