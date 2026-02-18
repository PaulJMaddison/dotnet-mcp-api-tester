using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Services;

public interface IRedactor
{
    TestPlan RedactPlan(TestPlan plan, IReadOnlyList<string>? patterns);
    TestRunResult RedactResult(TestRunResult result, IReadOnlyList<string>? patterns);
    Dictionary<string, string> RedactHeaders(IReadOnlyDictionary<string, string> headers, IReadOnlyList<string>? patterns);
    string? RedactText(string? text, IReadOnlyList<string>? patterns);
}
