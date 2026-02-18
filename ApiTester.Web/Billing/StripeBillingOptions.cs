namespace ApiTester.Web.Billing;

public sealed class StripeBillingOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string CheckoutSuccessUrl { get; set; } = "https://example.test/billing/success";
    public string CheckoutCancelUrl { get; set; } = "https://example.test/billing/cancel";
    public string CustomerPortalReturnUrl { get; set; } = "https://example.test/billing";
    public string FreePriceId { get; set; } = "";
    public string ProPriceId { get; set; } = "";
    public string TeamPriceId { get; set; } = "";

    public bool BillingEnabled => !string.IsNullOrWhiteSpace(SecretKey);
    public bool WebhookEnabled => BillingEnabled && !string.IsNullOrWhiteSpace(WebhookSecret);
}
