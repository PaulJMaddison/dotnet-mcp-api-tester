using ApiTester.McpServer.Models;

namespace ApiTester.Web.Contracts;

public sealed record ComplianceReportResponse(
    Guid RunId,
    CompliancePolicySummary PolicySummary,
    ComplianceRunResultsSummary RunResults,
    ComplianceAuditTrailExcerpt AuditTrail,
    ComplianceRetentionStatement Retention,
    string? Narrative);

public sealed record CompliancePolicySummary(
    bool? DryRun,
    IReadOnlyList<string>? AllowedMethods,
    IReadOnlyList<string>? AllowedBaseUrls,
    bool? BlockLocalhost,
    bool? BlockPrivateNetworks,
    bool? RetryOnFlake,
    int? MaxRetries,
    int? TimeoutSeconds,
    int? MaxRequestBodyBytes,
    int? MaxResponseBodyBytes);

public sealed record ComplianceRunResultsSummary(
    int TotalCases,
    int Passed,
    int Failed,
    int BlockedExpected,
    int BlockedUnexpected,
    int Flaky,
    long TotalDurationMs,
    ResultClassificationSummary ClassificationSummary);

public sealed record ComplianceAuditTrailExcerpt(
    int TotalEvents,
    IReadOnlyList<AuditEventResponse> Events);

public sealed record ComplianceRetentionStatement(
    int? RetentionDays,
    IReadOnlyList<string> RedactionRules,
    bool RedactionApplied,
    string Statement);
