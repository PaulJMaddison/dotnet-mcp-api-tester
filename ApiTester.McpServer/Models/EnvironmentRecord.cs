namespace ApiTester.McpServer.Models;

public sealed record EnvironmentRecord(
    Guid EnvironmentId,
    Guid ProjectId,
    string OwnerKey,
    string Name,
    string BaseUrl,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
