using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileMembershipStore : IMembershipStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileMembershipStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "memberships.json");

    public async Task<MembershipRecord> CreateAsync(Guid organisationId, Guid userId, OrgRole role, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(m => m.OrganisationId == organisationId && m.UserId == userId);
            if (existing is not null)
                return existing;

            var record = new MembershipRecord(organisationId, userId, role, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<MembershipRecord?> GetAsync(Guid organisationId, Guid userId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(m => m.OrganisationId == organisationId && m.UserId == userId);
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByOrganisationAsync(Guid organisationId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.Where(m => m.OrganisationId == organisationId).ToList();
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByUserAsync(Guid userId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.Where(m => m.UserId == userId).ToList();
    }

    private async Task<List<MembershipRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<MembershipRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<MembershipRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
