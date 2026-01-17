using System.Text;
using ApiTester.Site.Content;

namespace ApiTester.Site.Tests;

public class MarketingContentTests
{
    [Fact]
    public void Current_IncludesRequiredNavigationRoutes()
    {
        var routes = MarketingContent.Current.Layout.Navigation.Select(item => item.Url).ToList();

        Assert.Contains("/qa-reporting", routes);
        Assert.Contains("/use-cases", routes);
        Assert.Contains("/about", routes);
    }

    [Fact]
    public void SecurityComplianceContent_IncludesMandatoryControls()
    {
        var controls = MarketingContent.Current.SecurityCompliance.Controls.Select(control => control.Title).ToList();

        Assert.Contains("SSRF guard", controls);
        Assert.Contains("Policy allowlists", controls);
        Assert.Contains("Audit trail", controls);
        Assert.Contains("Redaction", controls);
    }

    [Fact]
    public void HomeContent_CoversDeterministicPlansAndCiReadiness()
    {
        var content = MarketingContent.Current.Home;
        var combined = CombineText(
            content.HeroTitle,
            content.HeroSubtitle,
            content.WorkflowSubtitle,
            string.Join(" ", content.WorkflowSteps.Select(step => $"{step.Title} {step.Description}")),
            string.Join(" ", content.ProofPoints.Select(point => $"{point.Title} {point.Description}")));

        Assert.Contains("deterministic", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CI readiness", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run history", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeContent_IncludesVibeCoderPersona()
    {
        var personas = MarketingContent.Current.Home.PersonaTiles.Select(tile => tile.Title).ToList();

        Assert.Contains("Vibe coder", personas);
    }

    [Fact]
    public void QaReportingContent_DescribesEvidenceAndExports()
    {
        var content = MarketingContent.Current.QaReporting;
        var combined = CombineText(
            content.HeroTitle,
            content.HeroSubtitle,
            string.Join(" ", content.Highlights.Select(highlight => $"{highlight.Title} {highlight.Description}")));

        Assert.Contains("run history", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deterministic", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("export", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeveloperDocsContent_IncludesEnvironmentAndCodeSnippets()
    {
        var content = MarketingContent.Current.DeveloperDocs;

        Assert.NotEmpty(content.EnvironmentSteps);
        Assert.NotEmpty(content.CodeSnippets);
        Assert.All(content.CodeSnippets, snippet => Assert.False(string.IsNullOrWhiteSpace(snippet.Code)));
    }

    [Fact]
    public void PageMetadata_IsConfiguredForCorePages()
    {
        var pages = new[]
        {
            MarketingContent.Current.Home.Metadata,
            MarketingContent.Current.Pricing.Metadata,
            MarketingContent.Current.SecurityCompliance.Metadata,
            MarketingContent.Current.QaReporting.Metadata,
            MarketingContent.Current.DeveloperDocs.Metadata,
            MarketingContent.Current.UseCases.Metadata,
            MarketingContent.Current.About.Metadata
        };

        Assert.All(pages, page =>
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Title));
            Assert.False(string.IsNullOrWhiteSpace(page.Description));
            Assert.False(string.IsNullOrWhiteSpace(page.CanonicalUrl));
            Assert.False(string.IsNullOrWhiteSpace(page.OgImage));
        });
    }

    private static string CombineText(params string[] segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append(segment).Append(' ');
        }

        return builder.ToString();
    }
}
