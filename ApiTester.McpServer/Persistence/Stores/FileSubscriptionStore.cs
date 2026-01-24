using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileSubscriptionStore : ISubscriptionStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileSubscriptionStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "subscriptions.json");

    public async Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(s => s.OrganisationId == organisationId);
            if (existing is null)
            {
                var created = CreateDefault(organisationId, nowUtc);
                list.Add(created);
                await SaveAsync(list, ct);
                return created;
            }

            var normalized = NormalizePeriod(existing, nowUtc);
            if (!ReferenceEquals(normalized, existing))
            {
                var index = list.IndexOf(existing);
                list[index] = normalized;
                await SaveAsync(list, ct);
            }

            return normalized;
        }
        finally
        {
            _mutex.Release();
        }
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

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(s => s.OrganisationId == organisationId);
            if (existing is null)
            {
                existing = CreateDefault(organisationId, nowUtc);
                list.Add(existing);
            }

            var normalized = NormalizePeriod(existing, nowUtc);

            if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0
                && normalized.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value)
                return null;

            if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0
                && normalized.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value)
                return null;

            if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0
                && normalized.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value)
                return null;

            var updated = normalized with
            {
                ProjectsUsed = normalized.ProjectsUsed + update.ProjectsDelta,
                RunsUsed = normalized.RunsUsed + update.RunsDelta,
                AiCallsUsed = normalized.AiCallsUsed + update.AiCallsDelta,
                UpdatedUtc = nowUtc
            };

            var index = list.IndexOf(existing);
            list[index] = updated;
            await SaveAsync(list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<SubscriptionRecord?> UpdateProjectsUsedAsync(Guid organisationId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(s => s.OrganisationId == organisationId);
            if (existing is null)
            {
                var created = CreateDefault(organisationId, nowUtc) with { ProjectsUsed = projectsUsed };
                list.Add(created);
                await SaveAsync(list, ct);
                return created;
            }

            var normalized = NormalizePeriod(existing, nowUtc);
            var updated = normalized with
            {
                ProjectsUsed = projectsUsed,
                UpdatedUtc = nowUtc
            };

            var index = list.IndexOf(existing);
            list[index] = updated;
            await SaveAsync(list, ct);
            return updated;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static SubscriptionRecord CreateDefault(Guid organisationId, DateTime nowUtc)
    {
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        return new SubscriptionRecord(
            organisationId,
            SubscriptionPlan.Free,
            SubscriptionStatus.Active,
            true,
            periodStart,
            periodEnd,
            0,
            0,
            0,
            nowUtc);
    }

    private static SubscriptionRecord NormalizePeriod(SubscriptionRecord record, DateTime nowUtc)
    {
        if (nowUtc < record.PeriodEndUtc)
            return record;

        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        return record with
        {
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
            RunsUsed = 0,
            AiCallsUsed = 0,
            UpdatedUtc = nowUtc
        };
    }

    private async Task<List<SubscriptionRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<SubscriptionRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<SubscriptionRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
