namespace ApiTester.McpServer.Models;

public sealed record OpenApiSpecRecord(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    string SpecJson,
    DateTime CreatedUtc);
