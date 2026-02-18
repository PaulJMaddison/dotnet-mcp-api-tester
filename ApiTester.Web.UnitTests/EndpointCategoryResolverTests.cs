using ApiTester.Web.AbuseProtection;

namespace ApiTester.Web.UnitTests;

public class EndpointCategoryResolverTests
{
    [Theory]
    [InlineData("/api/ai/explain", EndpointCategory.Ai)]
    [InlineData("/api/v1/ai/suggest-tests", EndpointCategory.Ai)]
    [InlineData("/ai/summarise-run", EndpointCategory.Ai)]
    [InlineData("/api/projects/a/runs/execute/b", EndpointCategory.RunExecution)]
    [InlineData("/api/v1/projects/a", EndpointCategory.Default)]
    public void Resolve_ReturnsExpectedCategory(string path, EndpointCategory expected)
    {
        var category = EndpointCategoryResolver.Resolve(path);

        Assert.Equal(expected, category);
    }
}
