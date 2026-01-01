using Microsoft.Extensions.Options;

namespace ApiTester.Web.Entitlements;

public enum SubscriptionTier
{
    Free,
    Pro
}

public sealed class EntitlementOptions
{
    public string Tier { get; init; } = "Free";
}

public sealed class EntitlementService
{
    private readonly SubscriptionTier _tier;

    public EntitlementService(IOptions<EntitlementOptions> options)
    {
        var tierRaw = options.Value?.Tier;
        _tier = Enum.TryParse<SubscriptionTier>(tierRaw, true, out var parsed)
            ? parsed
            : SubscriptionTier.Free;
    }

    public SubscriptionTier Tier => _tier;

    public bool CanUseAi => _tier == SubscriptionTier.Pro;

    public bool CanExport => _tier == SubscriptionTier.Pro;
}
