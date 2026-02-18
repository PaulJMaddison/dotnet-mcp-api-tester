using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class RedactionServiceTests
{
    [Fact]
    public void RedactPlan_MasksAuthHeadersAndBodies()
    {
        var service = new RedactionService();
        var plan = new TestPlan
        {
            OperationId = "op",
            Method = "POST",
            PathTemplate = "/v1/thing",
            Cases = new List<TestCase>
            {
                new()
                {
                    Name = "Auth",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Authorization"] = "Bearer super-secret",
                        ["X-Trace"] = "token=abc123"
                    },
                    BodyJson = "{\"token\":\"abc123\"}",
                    ExpectedStatusCodes = new List<int> { 200 }
                }
            }
        };

        var patterns = new List<string>
        {
            "token\\s*[:=]\\s*\\\"?\\w+\\\"?"
        };

        var redacted = service.RedactPlan(plan, patterns);
        var redactedCase = redacted.Cases.Single();

        Assert.Equal("[REDACTED]", redactedCase.Headers["Authorization"]);
        Assert.Contains("[REDACTED]", redactedCase.Headers["X-Trace"]);
        Assert.Contains("[REDACTED]", redactedCase.BodyJson);
    }

    [Fact]
    public void RedactResult_AppliesPatternToResponseSnippet()
    {
        var service = new RedactionService();
        var result = new TestRunResult
        {
            OperationId = "op",
            TotalCases = 1,
            Passed = 1,
            Failed = 0,
            Blocked = 0,
            TotalDurationMs = 10,
            Results = new List<TestCaseResult>
            {
                new()
                {
                    Name = "case",
                    Pass = true,
                    ResponseSnippet = "token=abc123"
                }
            }
        };

        var patterns = new List<string>
        {
            "token\\s*[:=]\\s*\\w+"
        };

        var redacted = service.RedactResult(result, patterns);
        Assert.Contains("[REDACTED]", redacted.Results.Single().ResponseSnippet);
    }
    [Fact]
    public void RedactText_DefaultRulesMaskHeadersQueryAndJsonSecrets()
    {
        var service = new RedactionService();
        var input = "Authorization: Bearer super-secret; token=abc123&api_key=key123 {\"clientSecret\":\"topsecret\",\"password\":\"p4ss\"}";

        var output = service.RedactText(input, patterns: null);

        Assert.NotNull(output);
        Assert.DoesNotContain("super-secret", output);
        Assert.DoesNotContain("abc123", output);
        Assert.DoesNotContain("key123", output);
        Assert.DoesNotContain("topsecret", output);
        Assert.DoesNotContain("p4ss", output);
        Assert.Contains("[REDACTED]", output);
    }

}
