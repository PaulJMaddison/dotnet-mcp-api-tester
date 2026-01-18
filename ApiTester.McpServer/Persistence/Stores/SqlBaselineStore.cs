using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlBaselineStore : IBaselineStore
{
    private readonly ApiTesterDbContext _db;

    public SqlBaselineStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<BaselineRecord?> GetAsync(Guid organisationId, string projectKey, string operationId, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? string.Empty : operationId.Trim();

        var baseline = await _db.BaselineRuns
            .AsNoTracking()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(
                x => x.Project != null
                     && x.Project.ProjectKey == projectKey
                     && x.Project.OrganisationId == organisationId
                     && x.OperationId == operationId,
                ct);

        return baseline is null
            ? null
            : new BaselineRecord(
                baseline.RunId,
                projectKey,
                baseline.OperationId,
                new DateTimeOffset(baseline.SetUtc, TimeSpan.Zero));
    }

    public async Task<IReadOnlyList<BaselineRecord>> ListAsync(Guid organisationId, string? projectKey, string? operationId, int take, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? null : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        take = take <= 0 ? 50 : Math.Min(take, 500);

        var query = _db.BaselineRuns
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.Project != null && x.Project.OrganisationId == organisationId);

        if (projectKey is not null)
            query = query.Where(x => x.Project != null && x.Project.ProjectKey == projectKey);

        if (operationId is not null)
            query = query.Where(x => x.OperationId == operationId);

        var items = await query
            .OrderByDescending(x => x.SetUtc)
            .Take(take)
            .Select(x => new BaselineRecord(
                x.RunId,
                x.Project != null ? x.Project.ProjectKey : projectKey ?? "default",
                x.OperationId,
                new DateTimeOffset(x.SetUtc, TimeSpan.Zero)))
            .ToListAsync(ct);

        return items;
    }

    public async Task<BaselineRecord?> SetAsync(Guid organisationId, string projectKey, string operationId, Guid runId, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? string.Empty : operationId.Trim();

        var projectId = await _db.Projects
            .Where(x => x.OrganisationId == organisationId && x.ProjectKey == projectKey)
            .Select(x => x.ProjectId)
            .FirstOrDefaultAsync(ct);

        if (projectId == Guid.Empty)
            return null;

        var existing = await _db.BaselineRuns
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.OperationId == operationId, ct);

        if (existing is null)
        {
            existing = new BaselineRunEntity
            {
                ProjectId = projectId,
                OperationId = operationId,
                RunId = runId,
                SetUtc = DateTime.UtcNow
            };
            _db.BaselineRuns.Add(existing);
        }
        else
        {
            existing.RunId = runId;
            existing.SetUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new BaselineRecord(runId, projectKey, operationId, new DateTimeOffset(existing.SetUtc, TimeSpan.Zero));
    }

    private static Guid NormalizeOrganisationId(Guid organisationId)
        => organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;
}
