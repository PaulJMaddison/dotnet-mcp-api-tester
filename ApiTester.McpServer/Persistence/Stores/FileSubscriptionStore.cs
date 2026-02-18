using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileSubscriptionStore : ISubscriptionStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private string SubscriptionPath => Path.Combine("run-history", "subscriptions.json");
    private string UsagePath => Path.Combine("run-history", "usage-counters.json");

    public FileSubscriptionStore(ApiRuntimeConfig cfg)
    {
    }

    public async Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadSubscriptionsAsync(ct);
            var existing = list.FirstOrDefault(x => x.OrganisationId == organisationId);
            if (existing is not null)
                return existing;

            var created = CreateDefaultSubscription(organisationId, nowUtc);
            list.Add(created);
            await SaveAsync(SubscriptionPath, list, ct);
            return created;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<SubscriptionRecord> UpsertStripeAsync(Guid organisationId, Guid tenantId, SubscriptionPlan plan, SubscriptionStatus status, bool renews, string? stripeCustomerId, string? stripeSubscriptionId, DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadSubscriptionsAsync(ct);
            var existing = list.FirstOrDefault(x => x.OrganisationId == organisationId);
            var updated = new SubscriptionRecord(organisationId, tenantId, plan, status, renews, stripeCustomerId, stripeSubscriptionId, periodStartUtc, periodEndUtc, nowUtc);
            if (existing is null)
                list.Add(updated);
            else
                list[list.IndexOf(existing)] = updated;

            await SaveAsync(SubscriptionPath, list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UsageCounterRecord> GetOrCreateUsageAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadUsageAsync(ct);
            var (start, end) = CurrentPeriod(nowUtc);
            var existing = list.FirstOrDefault(x => x.TenantId == tenantId && x.PeriodStartUtc == start);
            if (existing is not null)
                return existing;

            var created = new UsageCounterRecord(tenantId, start, end, 0, 0, 0, 0, nowUtc);
            list.Add(created);
            await SaveAsync(UsagePath, list, ct);
            return created;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UsageCounterRecord?> TryConsumeAsync(Guid tenantId, SubscriptionUsageUpdate update, SubscriptionUsageLimits limits, DateTime nowUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadUsageAsync(ct);
            var (start, end) = CurrentPeriod(nowUtc);
            var existing = list.FirstOrDefault(x => x.TenantId == tenantId && x.PeriodStartUtc == start) ?? new UsageCounterRecord(tenantId, start, end, 0, 0, 0, 0, nowUtc);

            if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0 && existing.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value)
                return null;
            if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0 && existing.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value)
                return null;
            if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0 && existing.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value)
                return null;
            if (limits.MaxExportsPerPeriod.HasValue && update.ExportsDelta > 0 && existing.ExportsUsed + update.ExportsDelta > limits.MaxExportsPerPeriod.Value)
                return null;

            var updated = existing with
            {
                ProjectsUsed = existing.ProjectsUsed + update.ProjectsDelta,
                RunsUsed = existing.RunsUsed + update.RunsDelta,
                AiCallsUsed = existing.AiCallsUsed + update.AiCallsDelta,
                ExportsUsed = existing.ExportsUsed + update.ExportsDelta,
                UpdatedUtc = nowUtc
            };

            var idx = list.FindIndex(x => x.TenantId == tenantId && x.PeriodStartUtc == start);
            if (idx >= 0) list[idx] = updated; else list.Add(updated);
            await SaveAsync(UsagePath, list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UsageCounterRecord?> UpdateProjectsUsedAsync(Guid tenantId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
    {
        var usage = await GetOrCreateUsageAsync(tenantId, nowUtc, ct);
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadUsageAsync(ct);
            var updated = usage with { ProjectsUsed = projectsUsed, UpdatedUtc = nowUtc };
            var idx = list.FindIndex(x => x.TenantId == usage.TenantId && x.PeriodStartUtc == usage.PeriodStartUtc);
            if (idx >= 0) list[idx] = updated; else list.Add(updated);
            await SaveAsync(UsagePath, list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static SubscriptionRecord CreateDefaultSubscription(Guid organisationId, DateTime nowUtc)
    {
        var (start, end) = CurrentPeriod(nowUtc);
        return new SubscriptionRecord(organisationId, organisationId, SubscriptionPlan.Free, SubscriptionStatus.Active, true, null, null, start, end, nowUtc);
    }

    private static (DateTime Start, DateTime End) CurrentPeriod(DateTime nowUtc)
    {
        var start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private async Task<List<SubscriptionRecord>> LoadSubscriptionsAsync(CancellationToken ct) => await LoadAsync<SubscriptionRecord>(SubscriptionPath, ct);
    private async Task<List<UsageCounterRecord>> LoadUsageAsync(CancellationToken ct) => await LoadAsync<UsageCounterRecord>(UsagePath, ct);

    private static async Task<List<T>> LoadAsync<T>(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return [];
        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<T>>(json, JsonDefaults.Default) ?? [];
    }

    private static async Task SaveAsync<T>(string filePath, List<T> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(list, JsonDefaults.Default), ct);
    }
}
