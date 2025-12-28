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

        var entity = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(ct);

        return entity.ProjectId;
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
