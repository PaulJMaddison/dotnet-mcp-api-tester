namespace ApiTester.McpServer.Persistence.Entities;

public sealed class BillingWebhookEventEntity
{
    public Guid BillingWebhookEventEntityId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedUtc { get; set; }
}
