using System.Data;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlSubscriptionStore : ISubscriptionStore
{
    private readonly ApiTesterDbContext _db;

    public SqlSubscriptionStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var entity = await _db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId, ct);
        if (entity is null)
        {
            entity = CreateDefault(organisationId, nowUtc);
            _db.Subscriptions.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Map(entity);
        }

        NormalizePeriod(entity, nowUtc);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<SubscriptionRecord?> TryConsumeAsync(
        Guid organisationId,
        SubscriptionUsageUpdate update,
        SubscriptionUsageLimits limits,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await _db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId, ct);
        if (entity is null)
        {
            entity = CreateDefault(organisationId, nowUtc);
            _db.Subscriptions.Add(entity);
        }

        NormalizePeriod(entity, nowUtc);

        if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0
            && entity.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value)
            return null;

        if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0
            && entity.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value)
            return null;

        if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0
            && entity.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value)
            return null;

        entity.ProjectsUsed += update.ProjectsDelta;
        entity.RunsUsed += update.RunsDelta;
        entity.AiCallsUsed += update.AiCallsDelta;
        entity.UpdatedUtc = nowUtc;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(entity);
    }

    public async Task<SubscriptionRecord?> UpdateProjectsUsedAsync(Guid organisationId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var entity = await _db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId, ct);
        if (entity is null)
        {
            entity = CreateDefault(organisationId, nowUtc);
            _db.Subscriptions.Add(entity);
        }

        NormalizePeriod(entity, nowUtc);
        entity.ProjectsUsed = projectsUsed;
        entity.UpdatedUtc = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static SubscriptionEntity CreateDefault(Guid organisationId, DateTime nowUtc)
    {
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new SubscriptionEntity
        {
            OrganisationId = organisationId,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            Renews = true,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodStart.AddMonths(1),
            ProjectsUsed = 0,
            RunsUsed = 0,
            AiCallsUsed = 0,
            UpdatedUtc = nowUtc
        };
    }

    private static void NormalizePeriod(SubscriptionEntity entity, DateTime nowUtc)
    {
        if (nowUtc < entity.PeriodEndUtc)
            return;

        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.PeriodStartUtc = periodStart;
        entity.PeriodEndUtc = periodStart.AddMonths(1);
        entity.RunsUsed = 0;
        entity.AiCallsUsed = 0;
        entity.UpdatedUtc = nowUtc;
    }

    private static SubscriptionRecord Map(SubscriptionEntity entity)
        => new(
            entity.OrganisationId,
            entity.Plan,
            entity.Status,
            entity.Renews,
            entity.PeriodStartUtc,
            entity.PeriodEndUtc,
            entity.ProjectsUsed,
            entity.RunsUsed,
            entity.AiCallsUsed,
            entity.UpdatedUtc);
}
