namespace ApiTester.McpServer.Persistence.Entities;

public sealed class UsageCounterEntity
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public int ProjectsUsed { get; set; }
    public int RunsUsed { get; set; }
    public int AiCallsUsed { get; set; }
    public int ExportsUsed { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
