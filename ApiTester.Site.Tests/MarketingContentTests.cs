using System.Text;
using ApiTester.Site.Content;

namespace ApiTester.Site.Tests;

public class MarketingContentTests
{
    [Fact]
    public void Current_IncludesRequiredNavigationRoutes()
    {
        var routes = MarketingContent.Current.Layout.Navigation.Select(item => item.Url).ToList();

        Assert.Contains("/pricing", routes);
        Assert.Contains("/security", routes);
        Assert.Contains("/qa-reporting", routes);
        Assert.Contains("/docs", routes);
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
    public void PricingContent_IncludesThreePlansAndUpgradeSignals()
    {
        var pricing = MarketingContent.Current.Pricing;

        Assert.Equal(3, pricing.Plans.Count);
        Assert.Contains(pricing.Plans, plan => plan.Name == "Free");
        Assert.Contains(pricing.Plans, plan => plan.Name == "Pro");
        Assert.Contains(pricing.Plans, plan => plan.Name == "Team");
        Assert.Contains(pricing.FeatureMatrix, feature => feature.Feature.Contains("Retention", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PricingContent_IncludesHeadingsAndCtas()
    {
        var pricing = MarketingContent.Current.Pricing;

        Assert.False(string.IsNullOrWhiteSpace(pricing.HeroTitle));
        Assert.False(string.IsNullOrWhiteSpace(pricing.ComparisonTitle));
        Assert.False(string.IsNullOrWhiteSpace(pricing.UseCasesTitle));
        Assert.False(string.IsNullOrWhiteSpace(pricing.FaqTitle));
        Assert.False(string.IsNullOrWhiteSpace(pricing.PrimaryCtaLabel));
        Assert.False(string.IsNullOrWhiteSpace(pricing.PrimaryCtaLink));
        Assert.False(string.IsNullOrWhiteSpace(pricing.SecondaryCtaLabel));
        Assert.False(string.IsNullOrWhiteSpace(pricing.SecondaryCtaLink));
        Assert.False(string.IsNullOrWhiteSpace(pricing.ComparisonCtaLabel));
        Assert.False(string.IsNullOrWhiteSpace(pricing.ComparisonCtaLink));
    }

    [Fact]
    public void PricingContent_PlansHaveCompleteDefinitions()
    {
        var pricing = MarketingContent.Current.Pricing;

        Assert.All(pricing.Plans, plan =>
        {
            Assert.False(string.IsNullOrWhiteSpace(plan.Name));
            Assert.False(string.IsNullOrWhiteSpace(plan.Tagline));
            Assert.False(string.IsNullOrWhiteSpace(plan.Price));
            Assert.False(string.IsNullOrWhiteSpace(plan.CtaLabel));
            Assert.False(string.IsNullOrWhiteSpace(plan.CtaLink));
            Assert.NotEmpty(plan.Highlights);
        });
    }

    [Fact]
    public void UseCasesContent_IncludesThreeAudienceStories()
    {
        var useCases = MarketingContent.Current.UseCases.CaseStudies;

        Assert.Equal(3, useCases.Count);
        Assert.Contains(useCases, story => story.Title.Contains("Developer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(useCases, story => story.Title.Contains("QA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(useCases, story => story.Title.Contains("Compliance", StringComparison.OrdinalIgnoreCase));
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
