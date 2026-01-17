using System.Reflection;
using ApiTester.Site.Components.Pages;
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
        new object[] { typeof(UseCases), "/use-cases" },
        new object[] { typeof(About), "/about" },
        new object[] { typeof(Contact), "/contact" },
        new object[] { typeof(ThankYou), "/contact/thanks" },
        new object[] { typeof(Status), "/status" },
        new object[] { typeof(Features), "/features" }
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
}
