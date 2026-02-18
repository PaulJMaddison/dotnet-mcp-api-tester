using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Entities;

public sealed class SubscriptionEntity
{
    public Guid OrganisationId { get; set; }
    public Guid TenantId { get; set; }
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool Renews { get; set; } = true;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
