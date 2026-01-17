using ApiTester.Site.Components.Pages;
using Bunit;

namespace ApiTester.Site.Tests;

public class StatusRenderingTests
{
    [Fact]
    public void StatusPage_RendersPlaceholderAndSupportLink()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<Status>();

        Assert.Contains("System status", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coming soon", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(cut.Find("a[href=\"mailto:support@apitester.example.com\"]"));
    }
}
