using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileRunAnnotationStore : IRunAnnotationStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileRunAnnotationStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-annotations.json");

    public async Task<IReadOnlyList<RunAnnotationRecord>> ListAsync(string ownerKey, Guid runId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        return list
            .Where(a => a.OwnerKey == ownerKey && a.RunId == runId)
            .OrderBy(a => a.CreatedUtc)
            .ToList();
    }

    public async Task<RunAnnotationRecord?> GetAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(a => a.OwnerKey == ownerKey && a.RunId == runId && a.AnnotationId == annotationId);
    }

    public async Task<RunAnnotationRecord> CreateAsync(string ownerKey, Guid runId, string note, string? jiraLink, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        note = NormalizeNote(note);
        jiraLink = NormalizeJiraLink(jiraLink);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var now = DateTime.UtcNow;
            var record = new RunAnnotationRecord(Guid.NewGuid(), runId, ownerKey, note, jiraLink, now, now);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<RunAnnotationRecord?> UpdateAsync(string ownerKey, Guid runId, Guid annotationId, string note, string? jiraLink, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        note = NormalizeNote(note);
        jiraLink = NormalizeJiraLink(jiraLink);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(a => a.OwnerKey == ownerKey && a.RunId == runId && a.AnnotationId == annotationId);
            if (existing is null)
                return null;

            var updated = existing with
            {
                Note = note,
                JiraLink = jiraLink,
                UpdatedUtc = DateTime.UtcNow
            };

            var index = list.FindIndex(a => a.OwnerKey == ownerKey && a.RunId == runId && a.AnnotationId == annotationId);
            if (index >= 0)
                list[index] = updated;

            await SaveAsync(list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> DeleteAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var removed = list.RemoveAll(a => a.OwnerKey == ownerKey && a.RunId == runId && a.AnnotationId == annotationId);
            if (removed == 0)
                return false;

            await SaveAsync(list, ct);
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<RunAnnotationRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var list = JsonSerializer.Deserialize<List<RunAnnotationRecord>>(json, JsonDefaults.Default) ?? [];
        return list.Select(NormalizeOwnerKey).ToList();
    }

    private async Task SaveAsync(List<RunAnnotationRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static RunAnnotationRecord NormalizeOwnerKey(RunAnnotationRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.OwnerKey))
            return record;

        return record with { OwnerKey = OwnerKeyDefaults.Default };
    }

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }

    private static string NormalizeNote(string note)
    {
        note = (note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Annotation note is required.", nameof(note));
        return note;
    }

    private static string? NormalizeJiraLink(string? jiraLink)
    {
        if (jiraLink is null)
            return null;

        jiraLink = jiraLink.Trim();
        if (string.IsNullOrWhiteSpace(jiraLink))
            throw new ArgumentException("Jira link cannot be empty.", nameof(jiraLink));

        return jiraLink;
    }
}
