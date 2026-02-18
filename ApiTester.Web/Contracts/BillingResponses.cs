namespace ApiTester.Web.Contracts;

public sealed record BillingPlanLimits(int MaxProjects, int MaxRunsPerPeriod, int MaxAiCallsPerPeriod);

public sealed record BillingPlanResponse(
    string Plan,
    string Status,
    bool Renews,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int RetentionDays,
    bool AiEnabled,
    bool AuditExportEnabled,
    BillingPlanLimits Limits);

public sealed record BillingUsageResponse(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    BillingPlanLimits Limits,
    BillingUsageCounters Usage);

public sealed record BillingUsageCounters(int ProjectsUsed, int RunsUsed, int AiCallsUsed, int ExportsUsed);


public sealed record BillingCheckoutRequest(string Plan);
public sealed record BillingCheckoutResponse(string Url);
public sealed record BillingPortalResponse(string Url);
