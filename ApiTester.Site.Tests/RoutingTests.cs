using System.Reflection;
using ApiTester.Site.Components.Pages;
using ApiTester.Site.Components.Pages.App;
using ApiTester.Site.Components.Pages.Docs;
using ApiTester.Site.Content;
using Microsoft.AspNetCore.Components;

namespace ApiTester.Site.Tests;

public class RoutingTests
{
    public static IEnumerable<object[]> Routes => new List<object[]>
    {
        new object[] { typeof(Home), "/" },
        new object[] { typeof(Pricing), "/pricing" },
        new object[] { typeof(Security), "/security" },
        new object[] { typeof(QaReporting), "/qa-reporting" },
        new object[] { typeof(Docs), "/docs" },
        new object[] { typeof(GettingStarted), "/docs/getting-started" },
        new object[] { typeof(ImportOpenApi), "/docs/import-openapi" },
        new object[] { typeof(PoliciesSsrf), "/docs/policies-ssrf" },
        new object[] { typeof(RunningTestPlans), "/docs/running-test-plans" },
        new object[] { typeof(RunHistory), "/docs/run-history" },
        new object[] { typeof(Persistence), "/docs/persistence" },
        new object[] { typeof(CiUsage), "/docs/ci-usage" },
        new object[] { typeof(ApiReference), "/docs/api-reference" },
        new object[] { typeof(UseCases), "/use-cases" },
        new object[] { typeof(About), "/about" },
        new object[] { typeof(Contact), "/contact" },
        new object[] { typeof(ThankYou), "/thank-you" },
        new object[] { typeof(ThankYou), "/contact/thanks" },
        new object[] { typeof(Status), "/status" },
        new object[] { typeof(Features), "/features" },
        new object[] { typeof(SignIn), "/app/sign-in" },
        new object[] { typeof(Onboarding), "/app/onboarding" },
        new object[] { typeof(Projects), "/app/projects" },
        new object[] { typeof(Leads), "/app/leads" },
        new object[] { typeof(ProjectRuns), "/app/projects/{projectKey}/runs" },
        new object[] { typeof(RunDetail), "/app/runs/{runId}" }
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void PagesExposeExpectedRoutes(Type pageType, string expectedRoute)
    {
        var routes = pageType.GetCustomAttributes<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains(expectedRoute, routes);
    }

    [Fact]
    public void NavigationRoutes_MapToKnownPages()
    {
        var expectedRoutes = Routes.Select(route => route[1]?.ToString())
            .Where(route => !string.IsNullOrWhiteSpace(route))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in MarketingContent.Current.Layout.Navigation)
        {
            Assert.Contains(item.Url, expectedRoutes);
        }
    }
}
