using System.Text.Json;
using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileProjectStore : IProjectStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FileProjectStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "projects.json");

    public async Task<ProjectRecord> CreateAsync(string name, CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var key = ProjectKeyGenerator.FromName(name);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(p => p.ProjectKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var record = new ProjectRecord(Guid.NewGuid(), name, key, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<PagedResult<ProjectRecord>> ListAsync(PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        var list = await LoadAsync(ct);

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

    public async Task<ProjectRecord?> GetAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.ProjectId == projectId);
    }

    private async Task<List<ProjectRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<ProjectRecord>>(json, JsonOptions) ?? [];
    }

    private async Task SaveAsync(List<ProjectRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
