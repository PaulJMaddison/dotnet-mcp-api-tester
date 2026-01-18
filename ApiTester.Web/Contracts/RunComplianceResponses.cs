namespace ApiTester.Web.Contracts;

public sealed record RunComplianceReportResponse(
    Guid RunId,
    ComplianceAuditMetadata Audit,
    CompliancePolicySection? Policy,
    ComplianceSsrfSection? Ssrf,
    ComplianceLimitSection? Limits);

public sealed record ComplianceAuditMetadata(
    string Actor,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    RunEnvironmentSnapshot? Environment);

public sealed record CompliancePolicySection(
    bool DryRun,
    IReadOnlyList<string> AllowedMethods,
    bool RetryOnFlake,
    int MaxRetries);

public sealed record ComplianceSsrfSection(
    IReadOnlyList<string> AllowedBaseUrls,
    bool BlockLocalhost,
    bool BlockPrivateNetworks);

public sealed record ComplianceLimitSection(
    int TimeoutSeconds,
    int MaxRequestBodyBytes,
    int MaxResponseBodyBytes);
