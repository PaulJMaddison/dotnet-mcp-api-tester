using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlProjectStore : IProjectStore
{
    private readonly ApiTesterDbContext _db;

    public SqlProjectStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateAsync(string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var key = ToProjectKey(name);

        var exists = await _db.Projects.AsNoTracking().AnyAsync(x => x.ProjectKey == key, ct);
        if (exists)
            throw new InvalidOperationException($"ProjectKey already exists: {key}");

        var entity = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            ProjectKey = key,
            Name = name,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(ct);

        return entity.ProjectId;
    }

    private static string ToProjectKey(string name)
    {
        // Simple, deterministic slug
        var s = name.Trim().ToLowerInvariant();
        var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var key = new string(chars);

        while (key.Contains("--", StringComparison.Ordinal))
            key = key.Replace("--", "-", StringComparison.Ordinal);

        key = key.Trim('-');

        return string.IsNullOrWhiteSpace(key) ? "default" : key;
    }

    public async Task<object> ListAsync(int take, CancellationToken ct)
    {
        take = take <= 0 ? 50 : Math.Min(take, 200);

        var projects = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .Select(x => new
            {
                projectId = x.ProjectId,
                name = x.Name,
                projectKey = x.ProjectKey,
                createdUtc = x.CreatedUtc
            })
            .ToListAsync(ct);

        return new { take, total = projects.Count, projects };
    }

    public async Task<bool> ExistsAsync(Guid projectId, CancellationToken ct)
    {
        return await _db.Projects.AsNoTracking().AnyAsync(x => x.ProjectId == projectId, ct);
    }
}
