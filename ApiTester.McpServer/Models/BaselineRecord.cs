namespace ApiTester.McpServer.Models;

public sealed record BaselineRecord(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset SetUtc);
