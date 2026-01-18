using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileUserStore : IUserStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileUserStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "users.json");

    public async Task<UserRecord> CreateAsync(string externalId, string displayName, string? email, CancellationToken ct)
    {
        externalId = NormalizeExternalId(externalId);
        displayName = (displayName ?? string.Empty).Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID is required.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(u => u.ExternalId.Equals(externalId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var record = new UserRecord(Guid.NewGuid(), externalId, displayName, email, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UserRecord?> GetAsync(Guid userId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(u => u.UserId == userId);
    }

    public async Task<UserRecord?> GetByExternalIdAsync(string externalId, CancellationToken ct)
    {
        externalId = NormalizeExternalId(externalId);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(u => u.ExternalId.Equals(externalId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<UserRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<UserRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<UserRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static string NormalizeExternalId(string externalId)
        => (externalId ?? string.Empty).Trim();
}
