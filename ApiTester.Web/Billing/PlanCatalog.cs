using ApiTester.McpServer.Models;

namespace ApiTester.Web.Billing;

public sealed record PlanLimits(
    int MaxProjects,
    int MaxRunsPerPeriod,
    int MaxAiCallsPerPeriod,
    int RetentionDays,
    bool AiEnabled,
    bool AuditExportEnabled);

public static class PlanCatalog
{
    // Commercial plan limits:
    // Free: 3 projects, 50 runs/month, 2 AI calls/month, 7-day retention window.
    // Pro: 20 projects, 500 runs/month, 200 AI calls/month, 30-day retention window (AI enabled).
    // Team: 100 projects, 2000 runs/month, 1000 AI calls/month, 90-day retention window (AI + audit export enabled).
    public static PlanLimits GetLimits(SubscriptionPlan plan)
        => plan switch
        {
            SubscriptionPlan.Free => new PlanLimits(
                MaxProjects: 3,
                MaxRunsPerPeriod: 50,
                MaxAiCallsPerPeriod: 2,
                RetentionDays: 7,
                AiEnabled: true,
                AuditExportEnabled: false),
            SubscriptionPlan.Pro => new PlanLimits(
                MaxProjects: 20,
                MaxRunsPerPeriod: 500,
                MaxAiCallsPerPeriod: 200,
                RetentionDays: 30,
                AiEnabled: true,
                AuditExportEnabled: false),
            SubscriptionPlan.Team => new PlanLimits(
                MaxProjects: 100,
                MaxRunsPerPeriod: 2000,
                MaxAiCallsPerPeriod: 1000,
                RetentionDays: 90,
                AiEnabled: true,
                AuditExportEnabled: true),
            _ => new PlanLimits(
                MaxProjects: 3,
                MaxRunsPerPeriod: 50,
                MaxAiCallsPerPeriod: 2,
                RetentionDays: 7,
                AiEnabled: true,
                AuditExportEnabled: false)
        };
}
