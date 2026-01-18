using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileOrganisationStore : IOrganisationStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileOrganisationStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "orgs.json");

    public async Task<OrganisationRecord> CreateAsync(string name, string slug, CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        slug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organisation name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Organisation slug is required.", nameof(slug));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(o => o.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var record = new OrganisationRecord(Guid.NewGuid(), name, slug, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<OrganisationRecord?> GetAsync(Guid organisationId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(o => o.OrganisationId == organisationId);
    }

    public async Task<OrganisationRecord?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(o => o.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<OrganisationRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<OrganisationRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<OrganisationRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static string NormalizeSlug(string slug)
        => (slug ?? string.Empty).Trim();
}
