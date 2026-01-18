using System.Net.Http;
using ApiTester.Site.Components.Pages;
using ApiTester.Site.Components.Pages.Docs;
using ApiTester.Site.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Site.Tests;

public class SeoMetadataTests
{
    public static IEnumerable<object[]> Pages => new List<object[]>
    {
        new object[] { typeof(Home) },
        new object[] { typeof(Features) },
        new object[] { typeof(Pricing) },
        new object[] { typeof(Security) },
        new object[] { typeof(QaReporting) },
        new object[] { typeof(Docs) },
        new object[] { typeof(GettingStarted) },
        new object[] { typeof(ImportOpenApi) },
        new object[] { typeof(RunningTestPlans) },
        new object[] { typeof(RunHistory) },
        new object[] { typeof(PoliciesSsrf) },
        new object[] { typeof(Persistence) },
        new object[] { typeof(CiUsage) },
        new object[] { typeof(ApiReference) },
        new object[] { typeof(UseCases) },
        new object[] { typeof(About) },
        new object[] { typeof(Contact) },
        new object[] { typeof(ThankYou) },
        new object[] { typeof(Status) }
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public void PagesExposeTitleAndDescription(Type componentType)
    {
        using var context = new TestContext();
        var cut = RenderPage(context, componentType);

        Assert.NotNull(cut.Find("title"));
        Assert.NotNull(cut.Find("meta[name=\"description\"]"));
    }

    private static IRenderedFragment RenderPage(TestContext context, Type componentType)
    {
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle")
            .SetResult(string.Empty);
        context.JSInterop.SetupVoid("Blazor._internal.PageTitle.setTitle");
        context.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost/") });
        context.Services.AddScoped<LeadCaptureClient>();

        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent(1, componentType);
            builder.CloseComponent();
        };

        return context.Render(fragment);
    }
}

public class SeoAssetTests
{
    [Fact]
    public void RobotsTxt_DeclaresSitemapAndAppDisallow()
    {
        var content = ReadSiteFile("robots.txt");

        Assert.Contains("Sitemap: https://apitester.example.com/sitemap.xml", content, StringComparison.Ordinal);
        Assert.Contains("Disallow: /app", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Sitemap_ListsPublicRoutes()
    {
        var content = ReadSiteFile("sitemap.xml");
        var expectedUrls = new[]
        {
            "https://apitester.example.com/",
            "https://apitester.example.com/features",
            "https://apitester.example.com/pricing",
            "https://apitester.example.com/security",
            "https://apitester.example.com/qa-reporting",
            "https://apitester.example.com/docs",
            "https://apitester.example.com/docs/getting-started",
            "https://apitester.example.com/docs/import-openapi",
            "https://apitester.example.com/docs/running-test-plans",
            "https://apitester.example.com/docs/run-history",
            "https://apitester.example.com/docs/policies-ssrf",
            "https://apitester.example.com/docs/persistence",
            "https://apitester.example.com/docs/ci-usage",
            "https://apitester.example.com/docs/api-reference",
            "https://apitester.example.com/use-cases",
            "https://apitester.example.com/about",
            "https://apitester.example.com/contact",
            "https://apitester.example.com/thank-you",
            "https://apitester.example.com/status"
        };

        foreach (var url in expectedUrls)
        {
            Assert.Contains($"<loc>{url}</loc>", content, StringComparison.Ordinal);
        }
    }

    private static string ReadSiteFile(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ApiTester.Site",
            "wwwroot",
            fileName));

        return File.ReadAllText(path);
    }
}
