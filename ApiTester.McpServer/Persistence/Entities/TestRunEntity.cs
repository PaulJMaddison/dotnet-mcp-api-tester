namespace ApiTester.McpServer.Persistence.Entities;

public sealed class TestRunEntity
{
    public Guid RunId { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public Guid? BaselineRunId { get; set; }
    public TestRunEntity? BaselineRun { get; set; }

    public string OperationId { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }

    public int TotalCases { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
    public long TotalDurationMs { get; set; }

    public List<TestCaseResultEntity> Results { get; set; } = new();
}
