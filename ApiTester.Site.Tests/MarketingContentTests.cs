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
        Assert.Contains("Audit logs", controls);
        Assert.Contains("Project separation", controls);
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
