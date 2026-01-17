using ApiTester.Site.Components.Pages;
using Bunit;

namespace ApiTester.Site.Tests;

public class SecurityComplianceRenderingTests
{
    [Fact]
    public void SecurityPage_RendersKeySectionsAndLinks()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<Security>();

        Assert.Contains("Security controls", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SSRF protection", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Data handling", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Audit trail", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Responsible disclosure", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status updates", cut.Markup, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(cut.Find("a[href=\"mailto:security@apitester.example.com\"]"));
        Assert.NotNull(cut.Find("a[href=\"/status\"]"));
        Assert.NotNull(cut.Find("a[href=\"/contact\"]"));
    }
}
