using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Reports;

public static class EvidencePackBuilder
{
    public static byte[] BuildZip(
        TestRunRecord run,
        OrganisationRecord? org,
        IReadOnlyList<AuditEventRecord> auditEvents,
        RedactionService redactionService,
        JsonSerializerOptions jsonOptions,
        DateTime createdUtc,
        string? signingKey)
    {
        var redactionRules = org?.RedactionRules ?? new List<string>();
        var sortedRun = SortRunDeterministically(RunExportRedactor.RedactRun(run, redactionService, redactionRules));
        var sortedAudit = SortAuditEvents(auditEvents)
            .Select(evt => new AuditEventRecord(
                evt.AuditEventId,
                evt.OrganisationId,
                evt.ActorUserId,
                evt.Action,
                evt.TargetType,
                evt.TargetId,
                evt.CreatedUtc,
                redactionService.RedactText(evt.MetadataJson, redactionRules) ?? evt.MetadataJson))
            .ToList();

        var runJson = RunExportGenerator.GenerateJson(sortedRun, jsonOptions);
        var reportMarkdown = RunReportGenerator.Generate(sortedRun, RunReportFormat.Markdown);
        var reportHtml = RunReportGenerator.Generate(sortedRun, RunReportFormat.Html);

        var policyJson = JsonSerializer.Serialize(new
        {
            sortedRun.RunId,
            sortedRun.PolicySnapshot
        }, jsonOptions);

        var auditPayload = new
        {
            sortedRun.RunId,
            Events = sortedAudit.Select(evt => new AuditEventResponse(
                evt.OrganisationId,
                evt.ActorUserId,
                evt.Action,
                evt.TargetType,
                evt.TargetId,
                evt.CreatedUtc,
                evt.MetadataJson)).ToList()
        };
        var auditJson = JsonSerializer.Serialize(auditPayload, jsonOptions);

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["audit.json"] = auditJson,
            ["policy-snapshot.json"] = policyJson,
            ["report.html"] = reportHtml,
            ["report.md"] = reportMarkdown,
            ["run.json"] = runJson
        };

        var manifest = new EvidenceManifest(
            createdUtc,
            files.Select(kvp => new EvidenceManifestFile(kvp.Key, ComputeSha256(kvp.Value))).ToList());
        var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var kvp in files)
                AddZipEntry(archive, kvp.Key, kvp.Value);

            AddZipEntry(archive, "manifest.json", manifestJson);

            if (!string.IsNullOrWhiteSpace(signingKey))
            {
                var signature = ComputeHmacSha256(manifestJson, signingKey);
                AddZipEntry(archive, "manifest.sig", signature);
            }
        }

        return stream.ToArray();
    }

    public static RunEvidenceAuditResponse BuildImmutableAudit(
        TestRunRecord run,
        OrganisationRecord? org,
        IReadOnlyList<AuditEventRecord> auditEvents,
        RedactionService redactionService,
        JsonSerializerOptions jsonOptions,
        DateTime createdUtc)
    {
        var redactionRules = org?.RedactionRules ?? new List<string>();
        var redactedRun = SortRunDeterministically(RunExportRedactor.RedactRun(run, redactionService, redactionRules));
        var runJson = RunExportGenerator.GenerateJson(redactedRun, jsonOptions);
        var immutableHash = ComputeSha256(runJson);

        var events = SortAuditEvents(auditEvents)
            .Select(evt => new AuditEventResponse(
                evt.OrganisationId,
                evt.ActorUserId,
                evt.Action,
                evt.TargetType,
                evt.TargetId,
                evt.CreatedUtc,
                redactionService.RedactText(evt.MetadataJson, redactionRules) ?? evt.MetadataJson))
            .ToList();

        return new RunEvidenceAuditResponse(
            redactedRun.RunId,
            redactedRun.StartedUtc,
            redactedRun.CompletedUtc,
            redactedRun.ProjectKey,
            redactedRun.OperationId,
            createdUtc,
            immutableHash,
            events);
    }

    private static IReadOnlyList<AuditEventRecord> SortAuditEvents(IReadOnlyList<AuditEventRecord> auditEvents)
        => auditEvents
            .OrderBy(evt => evt.CreatedUtc)
            .ThenBy(evt => evt.AuditEventId)
            .ToList();

    private static TestRunRecord SortRunDeterministically(TestRunRecord run)
    {
        var sortedResults = run.Result.Results
            .OrderBy(result => result.Name, StringComparer.Ordinal)
            .ThenBy(result => result.Method, StringComparer.Ordinal)
            .ThenBy(result => result.Url, StringComparer.Ordinal)
            .ToList();

        var sortedRunResult = new TestRunResult
        {
            OperationId = run.Result.OperationId,
            TotalCases = run.Result.TotalCases,
            Passed = run.Result.Passed,
            Failed = run.Result.Failed,
            Blocked = run.Result.Blocked,
            TotalDurationMs = run.Result.TotalDurationMs,
            ClassificationSummary = run.Result.ClassificationSummary,
            Results = sortedResults
        };

        return new TestRunRecord
        {
            RunId = run.RunId,
            OrganisationId = run.OrganisationId,
            Actor = run.Actor,
            Environment = run.Environment,
            PolicySnapshot = run.PolicySnapshot,
            OwnerKey = run.OwnerKey,
            OperationId = run.OperationId,
            SpecId = run.SpecId,
            BaselineRunId = run.BaselineRunId,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            Result = sortedRunResult,
            ProjectKey = run.ProjectKey
        };
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeHmacSha256(string content, string key)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AddZipEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }
}
