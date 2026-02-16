using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Billing;

public sealed record SubscriptionSnapshot(SubscriptionRecord Subscription, PlanLimits Limits)
{
    public bool IsActive => Subscription.Status == SubscriptionStatus.Active;
}

public sealed record SubscriptionGateResult(bool Allowed, string Title, string Detail, int StatusCode);

public sealed record RetentionWindow(int RetentionDays, DateTimeOffset CutoffUtc);

public sealed class SubscriptionEnforcementService
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly IProjectStore _projects;
    private readonly TimeProvider _timeProvider;

    public SubscriptionEnforcementService(
        ISubscriptionStore subscriptions,
        IProjectStore projects,
        TimeProvider timeProvider)
    {
        _subscriptions = subscriptions;
        _projects = projects;
        _timeProvider = timeProvider;
    }

    public async Task<SubscriptionSnapshot> GetSnapshotAsync(Guid organisationId, CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var record = await _subscriptions.GetOrCreateAsync(organisationId, nowUtc, ct);
        var limits = PlanCatalog.GetLimits(record.Plan);
        return new SubscriptionSnapshot(record, limits);
    }

    public async Task<RetentionWindow> GetRetentionWindowAsync(Guid organisationId, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(organisationId, ct);
        var cutoff = _timeProvider.GetUtcNow().AddDays(-snapshot.Limits.RetentionDays);
        return new RetentionWindow(snapshot.Limits.RetentionDays, cutoff);
    }

    public async Task<SubscriptionGateResult> CheckProjectCreateAsync(Guid organisationId, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(organisationId, ct);
        if (!snapshot.IsActive)
        {
            return new SubscriptionGateResult(
                false,
                "Subscription inactive",
                $"Organisation subscription status is {snapshot.Subscription.Status}.",
                StatusCodes.Status402PaymentRequired);
        }

        var result = await _projects.ListAsync(
            organisationId,
            new PageRequest(1, 0),
            SortField.CreatedUtc,
            SortDirection.Desc,
            ct);

        if (result.Total >= snapshot.Limits.MaxProjects)
        {
            return new SubscriptionGateResult(
                false,
                "Project limit reached",
                $"Plan {snapshot.Subscription.Plan} allows {snapshot.Limits.MaxProjects} projects.",
                StatusCodes.Status402PaymentRequired);
        }

        return new SubscriptionGateResult(true, string.Empty, string.Empty, StatusCodes.Status200OK);
    }

    public async Task UpdateProjectsUsedAsync(Guid organisationId, int totalProjects, CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _subscriptions.UpdateProjectsUsedAsync(organisationId, totalProjects, nowUtc, ct);
    }

    public async Task<SubscriptionGateResult> TryConsumeRunAsync(Guid organisationId, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(organisationId, ct);
        if (!snapshot.IsActive)
        {
            return new SubscriptionGateResult(
                false,
                "Subscription inactive",
                $"Organisation subscription status is {snapshot.Subscription.Status}.",
                StatusCodes.Status402PaymentRequired);
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var consumed = await _subscriptions.TryConsumeAsync(
            organisationId,
            new SubscriptionUsageUpdate(0, 1, 0),
            new SubscriptionUsageLimits(snapshot.Limits.MaxProjects, snapshot.Limits.MaxRunsPerPeriod, snapshot.Limits.MaxAiCallsPerPeriod),
            nowUtc,
            ct);

        if (consumed is null)
        {
            return new SubscriptionGateResult(
                false,
                "Run quota exceeded",
                $"Plan {snapshot.Subscription.Plan} allows {snapshot.Limits.MaxRunsPerPeriod} runs per month.",
                StatusCodes.Status402PaymentRequired);
        }

        return new SubscriptionGateResult(true, string.Empty, string.Empty, StatusCodes.Status200OK);
    }

    public async Task<SubscriptionGateResult> TryConsumeAiAsync(Guid organisationId, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(organisationId, ct);
        if (!snapshot.IsActive)
        {
            return new SubscriptionGateResult(
                false,
                "Subscription inactive",
                $"Organisation subscription status is {snapshot.Subscription.Status}.",
                StatusCodes.Status402PaymentRequired);
        }

        if (!snapshot.Limits.AiEnabled || snapshot.Limits.MaxAiCallsPerPeriod <= 0)
        {
            return new SubscriptionGateResult(
                false,
                "AI not available",
                $"AI features are unavailable for the current plan: {snapshot.Subscription.Plan}.",
                StatusCodes.Status403Forbidden);
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var consumed = await _subscriptions.TryConsumeAsync(
            organisationId,
            new SubscriptionUsageUpdate(0, 0, 1),
            new SubscriptionUsageLimits(snapshot.Limits.MaxProjects, snapshot.Limits.MaxRunsPerPeriod, snapshot.Limits.MaxAiCallsPerPeriod),
            nowUtc,
            ct);

        if (consumed is null)
        {
            return new SubscriptionGateResult(
                false,
                "AI quota exceeded",
                $"Plan {snapshot.Subscription.Plan} allows {snapshot.Limits.MaxAiCallsPerPeriod} AI calls per month.",
                StatusCodes.Status402PaymentRequired);
        }

        return new SubscriptionGateResult(true, string.Empty, string.Empty, StatusCodes.Status200OK);
    }

    public async Task<SubscriptionGateResult> CheckExportAccessAsync(Guid organisationId, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(organisationId, ct);
        if (!snapshot.IsActive)
        {
            return new SubscriptionGateResult(
                false,
                "Subscription inactive",
                $"Organisation subscription status is {snapshot.Subscription.Status}.",
                StatusCodes.Status402PaymentRequired);
        }

        if (!snapshot.Limits.AuditExportEnabled)
        {
            return new SubscriptionGateResult(
                false,
                "Export not available",
                $"Export features require a Team subscription. Current plan: {snapshot.Subscription.Plan}.",
                StatusCodes.Status403Forbidden);
        }

        return new SubscriptionGateResult(true, string.Empty, string.Empty, StatusCodes.Status200OK);
    }
}
