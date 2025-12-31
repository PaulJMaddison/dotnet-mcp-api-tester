using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileProjectStore : IProjectStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileProjectStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "projects.json");

    public async Task<ProjectRecord> CreateAsync(string ownerKey, string name, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var key = ProjectKeyGenerator.FromName(name);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(p => p.OwnerKey == ownerKey && p.ProjectKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var record = new ProjectRecord(Guid.NewGuid(), ownerKey, name, key, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<PagedResult<ProjectRecord>> ListAsync(string ownerKey, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        list = list.Where(p => p.OwnerKey == ownerKey).ToList();

        var ordered = sortField switch
        {
            SortField.StartedUtc => direction == SortDirection.Asc
                ? list.OrderBy(p => p.CreatedUtc)
                : list.OrderByDescending(p => p.CreatedUtc),
            _ => direction == SortDirection.Asc
                ? list.OrderBy(p => p.CreatedUtc)
                : list.OrderByDescending(p => p.CreatedUtc)
        };

        var total = list.Count;
        var page = ordered
            .Skip(request.Offset)
            .Take(request.PageSize)
            .ToList();

        int? nextOffset = request.Offset + page.Count < total
            ? request.Offset + page.Count
            : null;

        return new PagedResult<ProjectRecord>(page, total, nextOffset);
    }

    public async Task<ProjectRecord?> GetAsync(string ownerKey, Guid projectId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.ProjectId == projectId && p.OwnerKey == ownerKey);
    }

    public async Task<ProjectRecord?> GetByKeyAsync(string ownerKey, string projectKey, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        projectKey = NormalizeProjectKey(projectKey);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.OwnerKey == ownerKey && p.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> ExistsAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.Any(p => p.ProjectId == projectId);
    }

    private async Task<List<ProjectRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var list = JsonSerializer.Deserialize<List<ProjectRecord>>(json, JsonDefaults.Default) ?? [];
        return list.Select(NormalizeOwnerKey).ToList();
    }

    private async Task SaveAsync(List<ProjectRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static ProjectRecord NormalizeOwnerKey(ProjectRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.OwnerKey))
            return record;

        return record with { OwnerKey = OwnerKeyDefaults.Default };
    }

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }

    private static string NormalizeProjectKey(string projectKey)
    {
        projectKey = (projectKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey;
    }
}
