using ApiTester.McpServer.Models;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class ResultClassificationRulesTests
{
    [Fact]
    public void Classify_ReturnsBlockedExpected_ForMissingRequiredParam()
    {
        var result = new TestCaseResult
        {
            Name = "Missing required path param 'code'",
            Blocked = true,
            BlockReason = "Missing required path param(s): code",
            Pass = false
        };

        var classification = ResultClassificationRules.Classify(result);

        Assert.Equal(ResultClassification.BlockedExpected, classification);
    }

    [Fact]
    public void Classify_ReturnsBlockedUnexpected_ForPolicyBlock()
    {
        var result = new TestCaseResult
        {
            Name = "Blocked by policy",
            Blocked = true,
            BlockReason = "No allowedBaseUrls configured, deny by default.",
            Pass = false
        };

        var classification = ResultClassificationRules.Classify(result);

        Assert.Equal(ResultClassification.BlockedUnexpected, classification);
    }

    [Fact]
    public void Classify_ReturnsFlakyExternal_ForHttpbin502()
    {
        var result = new TestCaseResult
        {
            Name = "httpbin flake",
            Blocked = false,
            Pass = false,
            StatusCode = 502,
            FailureReason = "Expected [200] but got 502."
        };

        var classification = ResultClassificationRules.Classify(result);

        Assert.Equal(ResultClassification.FlakyExternal, classification);
        Assert.True(result.IsFlaky);
        Assert.Equal("upstream502", result.FlakeReasonCategory);
    }

    [Fact]
    public void Classify_ReturnsFlakyExternal_For502ResponseSnippet()
    {
        var result = new TestCaseResult
        {
            Name = "502 snippet",
            Blocked = false,
            Pass = false,
            ResponseSnippet = "502 Bad Gateway"
        };

        var classification = ResultClassificationRules.Classify(result);

        Assert.Equal(ResultClassification.FlakyExternal, classification);
        Assert.True(result.IsFlaky);
        Assert.Equal("upstream502", result.FlakeReasonCategory);
    }

    [Fact]
    public void Classify_ReturnsFail_ForNonFlakyFailure()
    {
        var result = new TestCaseResult
        {
            Name = "Failure",
            Blocked = false,
            Pass = false,
            StatusCode = 500,
            FailureReason = "Expected [200] but got 500."
        };

        var classification = ResultClassificationRules.Classify(result);

        Assert.Equal(ResultClassification.Fail, classification);
    }

    [Fact]
    public void Summarize_ComputesClassificationCounts()
    {
        var results = new List<TestCaseResult>
        {
            new()
            {
                Name = "Pass",
                Blocked = false,
                Pass = true
            },
            new()
            {
                Name = "Expected block",
                Blocked = true,
                BlockReason = "Missing required path param(s): code",
                Pass = false
            },
            new()
            {
                Name = "Unexpected block",
                Blocked = true,
                BlockReason = "No baseUrl configured. Call api_set_base_url first.",
                Pass = false
            },
            new()
            {
                Name = "Flaky",
                Blocked = false,
                Pass = false,
                StatusCode = 502
            },
            new()
            {
                Name = "Fail",
                Blocked = false,
                Pass = false,
                StatusCode = 500
            }
        };

        var summary = ResultClassificationRules.Summarize(results);

        Assert.Equal(1, summary.Pass);
        Assert.Equal(1, summary.BlockedExpected);
        Assert.Equal(1, summary.BlockedUnexpected);
        Assert.Equal(1, summary.FlakyExternal);
        Assert.Equal(1, summary.Fail);
    }
}
