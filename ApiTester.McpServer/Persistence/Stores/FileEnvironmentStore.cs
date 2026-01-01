using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileEnvironmentStore : IEnvironmentStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileEnvironmentStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "environments.json");

    public async Task<EnvironmentRecord> CreateAsync(string ownerKey, Guid projectId, string name, string baseUrl, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        baseUrl = NormalizeBaseUrl(baseUrl);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            if (list.Any(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Environment name already exists.");

            var now = DateTime.UtcNow;
            var record = new EnvironmentRecord(Guid.NewGuid(), projectId, ownerKey, name, baseUrl, now, now);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<EnvironmentRecord>> ListAsync(string ownerKey, Guid projectId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        return list
            .Where(e => e.OwnerKey == ownerKey && e.ProjectId == projectId)
            .OrderBy(e => e.Name)
            .ToList();
    }

    public async Task<EnvironmentRecord?> GetAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.EnvironmentId == environmentId);
    }

    public async Task<EnvironmentRecord?> GetByNameAsync(string ownerKey, Guid projectId, string name, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<EnvironmentRecord?> UpdateAsync(string ownerKey, Guid projectId, Guid environmentId, string name, string baseUrl, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        baseUrl = NormalizeBaseUrl(baseUrl);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.EnvironmentId == environmentId);
            if (existing is null)
                return null;

            if (list.Any(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.EnvironmentId != environmentId &&
                              e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Environment name already exists.");

            var updated = existing with
            {
                Name = name,
                BaseUrl = baseUrl,
                UpdatedUtc = DateTime.UtcNow
            };

            var index = list.FindIndex(e => e.EnvironmentId == environmentId && e.ProjectId == projectId && e.OwnerKey == ownerKey);
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

    public async Task<bool> DeleteAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var removed = list.RemoveAll(e => e.OwnerKey == ownerKey && e.ProjectId == projectId && e.EnvironmentId == environmentId);
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

    private async Task<List<EnvironmentRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var list = JsonSerializer.Deserialize<List<EnvironmentRecord>>(json, JsonDefaults.Default) ?? [];
        return list.Select(NormalizeOwnerKey).ToList();
    }

    private async Task SaveAsync(List<EnvironmentRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static EnvironmentRecord NormalizeOwnerKey(EnvironmentRecord record)
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

    private static string NormalizeName(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Environment name is required.", nameof(name));
        return name;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        baseUrl = (baseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Environment baseUrl is required.", nameof(baseUrl));
        return baseUrl.TrimEnd('/');
    }
}
