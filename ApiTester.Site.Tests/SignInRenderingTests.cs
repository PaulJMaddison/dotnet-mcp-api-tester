using ApiTester.Site.Components.Pages.App;
using Bunit;

namespace ApiTester.Site.Tests;

public class SignInRenderingTests
{
    [Fact]
    public void SignIn_RendersRedirectOnlyMarkup()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SignIn>();

        Assert.DoesNotContain("api-key", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
