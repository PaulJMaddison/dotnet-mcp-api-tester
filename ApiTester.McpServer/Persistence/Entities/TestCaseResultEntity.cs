using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Entities;

public sealed class TestCaseResultEntity
{
    public long TestCaseResultId { get; set; }

    public Guid RunId { get; set; }
    public TestRunEntity? Run { get; set; }

    public string Name { get; set; } = "";
    public bool Blocked { get; set; }
    public string? BlockReason { get; set; }

    public string Method { get; set; } = "";
    public string? Url { get; set; }

    public int? StatusCode { get; set; }
    public long DurationMs { get; set; }

    public bool Pass { get; set; }
    public string? FailureReason { get; set; }
    public string? ResponseSnippet { get; set; }

    public ResultClassification? Classification { get; set; }
}
