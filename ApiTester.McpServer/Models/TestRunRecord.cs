using System.Text.Json.Serialization;

namespace ApiTester.McpServer.Models;

public sealed class TestRunRecord
{
  

    public Guid RunId { get; set; }
    public string OperationId { get; set; } = "";
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }

    public TestRunResult Result { get; set; }
}
