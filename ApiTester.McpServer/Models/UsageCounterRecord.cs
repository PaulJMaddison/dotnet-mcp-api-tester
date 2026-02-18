namespace ApiTester.McpServer.Models;

public sealed record UsageCounterRecord(
    Guid TenantId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int ProjectsUsed,
    int RunsUsed,
    int AiCallsUsed,
    int ExportsUsed,
    DateTime UpdatedUtc);
