using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Reports;

public static class ComplianceReportBuilder
{
    public static ComplianceReportResponse Build(
        TestRunRecord run,
        OrganisationRecord? org,
        IReadOnlyList<AuditEventRecord> auditEvents,
        RedactionService redactionService,
        string? narrative = null)
    {
        var redactionRules = org?.RedactionRules ?? new List<string>();
        var redactedRun = RunExportRedactor.RedactRun(run, redactionService, redactionRules);
        var policy = redactedRun.PolicySnapshot;
        var policySummary = new CompliancePolicySummary(
            policy?.DryRun,
            policy?.AllowedMethods?.ToList(),
            policy?.AllowedBaseUrls?.ToList(),
            policy?.BlockLocalhost,
            policy?.BlockPrivateNetworks,
            policy?.RetryOnFlake,
            policy?.MaxRetries,
            policy?.TimeoutSeconds,
            policy?.MaxRequestBodyBytes,
            policy?.MaxResponseBodyBytes);

        var classification = redactedRun.Result.ClassificationSummary;
        var runResults = new ComplianceRunResultsSummary(
            redactedRun.Result.TotalCases,
            classification.Pass,
            classification.Fail,
            classification.BlockedExpected,
            classification.BlockedUnexpected,
            classification.FlakyExternal,
            redactedRun.Result.TotalDurationMs,
            classification);

        var redactedAuditEvents = auditEvents
            .Select(evt => RedactAuditEvent(evt, redactionService, redactionRules))
            .Select(evt => new AuditEventResponse(
                evt.OrganisationId,
                evt.ActorUserId,
                evt.Action,
                evt.TargetType,
                evt.TargetId,
                evt.CreatedUtc,
                evt.MetadataJson))
            .ToList();

        var auditTrail = new ComplianceAuditTrailExcerpt(redactedAuditEvents.Count, redactedAuditEvents);

        var retentionDays = org?.RetentionDays;
        var hasRedaction = redactionRules.Count > 0;
        var retentionStatement = BuildRetentionStatement(retentionDays, hasRedaction);
        var retention = new ComplianceRetentionStatement(
            retentionDays,
            redactionRules.ToList(),
            hasRedaction,
            retentionStatement);

        return new ComplianceReportResponse(
            redactedRun.RunId,
            policySummary,
            runResults,
            auditTrail,
            retention,
            narrative);
    }

    private static AuditEventRecord RedactAuditEvent(
        AuditEventRecord record,
        RedactionService redactionService,
        IReadOnlyList<string> redactionRules)
    {
        var redactedMetadata = redactionService.RedactText(record.MetadataJson, redactionRules);
        return record with { MetadataJson = redactedMetadata };
    }

    private static string BuildRetentionStatement(int? retentionDays, bool hasRedaction)
    {
        var retentionLine = retentionDays.HasValue && retentionDays.Value > 0
            ? $"Retention setting configured for {retentionDays.Value} days."
            : "Retention setting not configured.";
        var redactionLine = hasRedaction
            ? "Redaction rules are configured and applied to this report."
            : "No redaction rules are configured for this report.";

        return $"{retentionLine} {redactionLine}";
    }
}
