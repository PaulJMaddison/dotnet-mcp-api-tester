using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileApiKeyStore : IApiKeyStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileApiKeyStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "api-keys.json");

    public async Task<ApiKeyRecord> CreateAsync(
        Guid organisationId,
        Guid userId,
        string name,
        string scopes,
        DateTime? expiresUtc,
        string hash,
        string prefix,
        CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        scopes = (scopes ?? string.Empty).Trim();
        hash = (hash ?? string.Empty).Trim();
        prefix = (prefix ?? string.Empty).Trim();

        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(scopes))
            throw new ArgumentException("Scopes are required.", nameof(scopes));
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash is required.", nameof(hash));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix is required.", nameof(prefix));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            if (list.Any(k => k.Prefix.Equals(prefix, StringComparison.Ordinal)))
                throw new InvalidOperationException("API key prefix already exists.");

            var record = new ApiKeyRecord(Guid.NewGuid(), organisationId, userId, name, scopes, expiresUtc, null, hash, prefix);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(Guid organisationId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            return Array.Empty<ApiKeyRecord>();

        var list = await LoadAsync(ct);
        return list
            .Where(k => k.OrganisationId == organisationId)
            .OrderBy(k => k.Name)
            .ToList();
    }

    public async Task<ApiKeyRecord?> GetAsync(Guid organisationId, Guid keyId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty || keyId == Guid.Empty)
            return null;

        var list = await LoadAsync(ct);
        return list.FirstOrDefault(k => k.OrganisationId == organisationId && k.KeyId == keyId);
    }

    public async Task<ApiKeyRecord?> GetByPrefixAsync(string prefix, CancellationToken ct)
    {
        prefix = (prefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var list = await LoadAsync(ct);
        return list.FirstOrDefault(k => k.Prefix.Equals(prefix, StringComparison.Ordinal));
    }

    public async Task<ApiKeyRecord?> RevokeAsync(Guid organisationId, Guid keyId, DateTime revokedUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty || keyId == Guid.Empty)
            return null;

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var record = list.FirstOrDefault(k => k.OrganisationId == organisationId && k.KeyId == keyId);
            if (record is null)
                return null;

            if (!record.RevokedUtc.HasValue)
            {
                record = record with { RevokedUtc = revokedUtc };
                var index = list.FindIndex(k => k.KeyId == keyId);
                if (index >= 0)
                    list[index] = record;
                await SaveAsync(list, ct);
            }

            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<ApiKeyRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<ApiKeyRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<ApiKeyRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
