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
            entity = CreateDefaultSubscription(organisationId, nowUtc);
            _db.Subscriptions.Add(entity);
            await _db.SaveChangesAsync(ct);
            return MapSubscription(entity);
        }

        return MapSubscription(entity);
    }

    public async Task<SubscriptionRecord> UpsertStripeAsync(Guid organisationId, Guid tenantId, SubscriptionPlan plan, SubscriptionStatus status, bool renews, string? stripeCustomerId, string? stripeSubscriptionId, DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc, CancellationToken ct)
    {
        var entity = await _db.Subscriptions.FirstOrDefaultAsync(s => s.OrganisationId == organisationId, ct);
        if (entity is null)
        {
            entity = CreateDefaultSubscription(organisationId, nowUtc);
            _db.Subscriptions.Add(entity);
        }

        entity.TenantId = tenantId;
        entity.Plan = plan;
        entity.Status = status;
        entity.Renews = renews;
        entity.StripeCustomerId = stripeCustomerId;
        entity.StripeSubscriptionId = stripeSubscriptionId;
        entity.PeriodStartUtc = periodStartUtc;
        entity.PeriodEndUtc = periodEndUtc;
        entity.UpdatedUtc = nowUtc;

        await _db.SaveChangesAsync(ct);
        return MapSubscription(entity);
    }

    public async Task<UsageCounterRecord> GetOrCreateUsageAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        var (periodStart, periodEnd) = CurrentPeriod(nowUtc);
        var entity = await _db.UsageCounters.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStart, ct);
        if (entity is null)
        {
            entity = new UsageCounterEntity
            {
                TenantId = tenantId,
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd,
                UpdatedUtc = nowUtc
            };
            _db.UsageCounters.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        return MapUsage(entity);
    }

    public async Task<UsageCounterRecord?> TryConsumeAsync(Guid tenantId, SubscriptionUsageUpdate update, SubscriptionUsageLimits limits, DateTime nowUtc, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        var (periodStart, periodEnd) = CurrentPeriod(nowUtc);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await _db.UsageCounters.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStart, ct);
        if (entity is null)
        {
            entity = new UsageCounterEntity
            {
                TenantId = tenantId,
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd,
                UpdatedUtc = nowUtc
            };
            _db.UsageCounters.Add(entity);
        }

        if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0 && entity.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value)
            return null;
        if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0 && entity.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value)
            return null;
        if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0 && entity.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value)
            return null;
        if (limits.MaxExportsPerPeriod.HasValue && update.ExportsDelta > 0 && entity.ExportsUsed + update.ExportsDelta > limits.MaxExportsPerPeriod.Value)
            return null;

        entity.ProjectsUsed += update.ProjectsDelta;
        entity.RunsUsed += update.RunsDelta;
        entity.AiCallsUsed += update.AiCallsDelta;
        entity.ExportsUsed += update.ExportsDelta;
        entity.UpdatedUtc = nowUtc;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapUsage(entity);
    }

    public async Task<UsageCounterRecord?> UpdateProjectsUsedAsync(Guid tenantId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
    {
        var usage = await GetOrCreateUsageAsync(tenantId, nowUtc, ct);
        var entity = await _db.UsageCounters.FirstAsync(u => u.TenantId == usage.TenantId && u.PeriodStartUtc == usage.PeriodStartUtc, ct);
        entity.ProjectsUsed = projectsUsed;
        entity.UpdatedUtc = nowUtc;
        await _db.SaveChangesAsync(ct);
        return MapUsage(entity);
    }

    private static (DateTime Start, DateTime End) CurrentPeriod(DateTime nowUtc)
    {
        var start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private static SubscriptionEntity CreateDefaultSubscription(Guid organisationId, DateTime nowUtc)
    {
        var (start, end) = CurrentPeriod(nowUtc);
        return new SubscriptionEntity
        {
            OrganisationId = organisationId,
            TenantId = organisationId,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            Renews = true,
            PeriodStartUtc = start,
            PeriodEndUtc = end,
            UpdatedUtc = nowUtc
        };
    }

    private static SubscriptionRecord MapSubscription(SubscriptionEntity entity)
        => new(
            entity.OrganisationId,
            entity.TenantId,
            entity.Plan,
            entity.Status,
            entity.Renews,
            entity.StripeCustomerId,
            entity.StripeSubscriptionId,
            entity.PeriodStartUtc,
            entity.PeriodEndUtc,
            entity.UpdatedUtc);

    private static UsageCounterRecord MapUsage(UsageCounterEntity entity)
        => new(
            entity.TenantId,
            entity.PeriodStartUtc,
            entity.PeriodEndUtc,
            entity.ProjectsUsed,
            entity.RunsUsed,
            entity.AiCallsUsed,
            entity.ExportsUsed,
            entity.UpdatedUtc);
}
