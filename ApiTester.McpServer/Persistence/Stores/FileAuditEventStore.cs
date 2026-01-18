using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileAuditEventStore : IAuditEventStore
{
    private readonly AppConfig _cfg;

    public FileAuditEventStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string RootPath => Path.Combine(_cfg.WorkingDirectory, "audit-log");

    private string OrgPath(Guid organisationId)
        => Path.Combine(RootPath, NormalizeOrganisationId(organisationId).ToString("N"));

    private static Guid NormalizeOrganisationId(Guid organisationId)
        => organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;

    public async Task<AuditEventRecord> CreateAsync(AuditEventRecord record, CancellationToken ct)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var createdUtc = record.CreatedUtc == default ? DateTime.UtcNow : record.CreatedUtc;
        var orgId = NormalizeOrganisationId(record.OrganisationId);
        var id = record.AuditEventId == Guid.Empty ? Guid.NewGuid() : record.AuditEventId;

        Directory.CreateDirectory(OrgPath(orgId));

        var filename = $"{createdUtc:yyyyMMddHHmmssffff}_{id:N}.json";
        var path = Path.Combine(OrgPath(orgId), filename);

        var normalized = new AuditEventRecord(
            id,
            orgId,
            record.ActorUserId,
            record.Action.Trim(),
            record.TargetType.Trim(),
            record.TargetId.Trim(),
            createdUtc,
            record.MetadataJson);

        var json = JsonSerializer.Serialize(normalized, JsonDefaults.Default);
        await File.WriteAllTextAsync(path, json, ct);

        return normalized;
    }

    public async Task<IReadOnlyList<AuditEventRecord>> ListAsync(
        Guid organisationId,
        int take,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        var dir = OrgPath(organisationId);

        if (!Directory.Exists(dir))
            return Array.Empty<AuditEventRecord>();

        var normalizedAction = string.IsNullOrWhiteSpace(action) ? null : action.Trim();

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.Name)
            .ToList();

        var results = new List<AuditEventRecord>(Math.Min(files.Count, take));

        foreach (var file in files)
        {
            if (results.Count >= take)
                break;

            var json = await File.ReadAllTextAsync(file.FullName, ct);
            var record = JsonSerializer.Deserialize<AuditEventRecord>(json, JsonDefaults.Default);
            if (record is null)
                continue;

            if (normalizedAction is not null && !string.Equals(record.Action, normalizedAction, StringComparison.Ordinal))
                continue;

            if (fromUtc.HasValue && record.CreatedUtc < fromUtc.Value)
                continue;

            if (toUtc.HasValue && record.CreatedUtc > toUtc.Value)
                continue;

            results.Add(record);
        }

        return results;
    }
}
