using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.Billing;

public sealed class StripeBillingService
{
    private readonly StripeBillingOptions _options;
    private readonly ISubscriptionStore _subscriptions;
    private readonly ApiTesterDbContext _db;
    private readonly TimeProvider _timeProvider;

    public StripeBillingService(IOptions<StripeBillingOptions> options, ISubscriptionStore subscriptions, ApiTesterDbContext db, TimeProvider timeProvider)
    {
        _options = options.Value;
        _subscriptions = subscriptions;
        _db = db;
        _timeProvider = timeProvider;
    }

    public Task<string> CreateCheckoutSessionAsync(Guid tenantId, string plan, CancellationToken ct)
    {
        var normalized = plan.Trim().ToLowerInvariant();
        if (normalized is not "pro" and not "team")
            throw new InvalidOperationException("Only Pro and Team can be purchased via checkout.");

        var checkoutToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var url = $"https://checkout.stripe.com/pay/{checkoutToken}?client_reference_id={tenantId:D}&plan={normalized}";
        return Task.FromResult(url);
    }

    public async Task<string> CreatePortalSessionAsync(Guid tenantId, CancellationToken ct)
    {
        var subscription = await _subscriptions.GetOrCreateAsync(tenantId, _timeProvider.GetUtcNow().UtcDateTime, ct);
        if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
            throw new InvalidOperationException("Stripe customer is not configured for tenant.");

        var portalToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(18)).ToLowerInvariant();
        return $"https://billing.stripe.com/p/session/{portalToken}?customer={Uri.EscapeDataString(subscription.StripeCustomerId)}";
    }

    public async Task<bool> HandleWebhookAsync(string json, string signatureHeader, CancellationToken ct)
    {
        ValidateSignature(json, signatureHeader, _options.WebhookSecret);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        var eventId = root.GetProperty("id").GetString();
        var eventType = root.GetProperty("type").GetString();
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("Webhook payload missing id/type.");

        if (await _db.BillingWebhookEvents.AnyAsync(x => x.EventId == eventId, ct))
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _db.BillingWebhookEvents.Add(new BillingWebhookEventEntity
        {
            BillingWebhookEventEntityId = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            ProcessedUtc = now
        });

        if (eventType is "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted")
        {
            var obj = root.GetProperty("data").GetProperty("object");
            var tenantId = ResolveTenantId(obj);
            var plan = ResolvePlan(obj);
            var status = ResolveStatus(obj.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : null);
            var renews = !(obj.TryGetProperty("cancel_at_period_end", out var cancelAtPeriod) && cancelAtPeriod.GetBoolean());
            var customerId = obj.TryGetProperty("customer", out var customer) ? customer.GetString() : null;
            var subscriptionId = obj.TryGetProperty("id", out var sid) ? sid.GetString() : null;

            var periodStart = DateTimeOffset.FromUnixTimeSeconds(obj.GetProperty("current_period_start").GetInt64()).UtcDateTime;
            var periodEnd = DateTimeOffset.FromUnixTimeSeconds(obj.GetProperty("current_period_end").GetInt64()).UtcDateTime;

            await _subscriptions.UpsertStripeAsync(tenantId, tenantId, plan, status, renews, customerId, subscriptionId, periodStart, periodEnd, now, ct);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateSignature(string payload, string signatureHeader, string secret)
    {
        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("t", out var tRaw) || !long.TryParse(tRaw, out var timestamp))
            throw new InvalidOperationException("Invalid Stripe-Signature header timestamp.");
        if (!parts.TryGetValue("v1", out var providedSignature))
            throw new InvalidOperationException("Invalid Stripe-Signature header signature.");

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(providedSignature)))
            throw new InvalidOperationException("Invalid Stripe webhook signature.");
    }

    private SubscriptionPlan ResolvePlan(JsonElement obj)
    {
        var priceId = string.Empty;
        if (obj.TryGetProperty("items", out var items)
            && items.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
            && data.GetArrayLength() > 0
            && data[0].TryGetProperty("price", out var price)
            && price.TryGetProperty("id", out var priceNode))
        {
            priceId = priceNode.GetString() ?? string.Empty;
        }

        return priceId switch
        {
            var id when string.Equals(id, _options.ProPriceId, StringComparison.OrdinalIgnoreCase) => SubscriptionPlan.Pro,
            var id when string.Equals(id, _options.TeamPriceId, StringComparison.OrdinalIgnoreCase) => SubscriptionPlan.Team,
            _ => SubscriptionPlan.Free
        };
    }

    private static Guid ResolveTenantId(JsonElement obj)
    {
        if (obj.TryGetProperty("metadata", out var meta)
            && meta.TryGetProperty("tenantId", out var tenantNode)
            && Guid.TryParse(tenantNode.GetString(), out var tenantId))
            return tenantId;

        if (obj.TryGetProperty("customer", out var customer)
            && Guid.TryParse(customer.GetString(), out var customerAsGuid))
            return customerAsGuid;

        throw new InvalidOperationException("Unable to resolve tenant ID from Stripe payload.");
    }

    private static SubscriptionStatus ResolveStatus(string? status)
        => status switch
        {
            "active" or "trialing" => SubscriptionStatus.Active,
            "past_due" or "unpaid" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.Canceled
        };
}
