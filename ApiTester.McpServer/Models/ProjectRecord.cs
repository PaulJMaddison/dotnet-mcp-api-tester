namespace ApiTester.McpServer.Models;

public sealed record ProjectRecord(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);
