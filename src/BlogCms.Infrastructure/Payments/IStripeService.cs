namespace BlogCms.Infrastructure.Payments;

public sealed record CheckoutSession(string Url, string SessionId);

/// <summary>
/// Normalized representation of the Stripe webhook events the app cares about.
/// </summary>
public sealed record StripeWebhookEvent(
    string Type,
    Guid? UserId = null,
    string? StripeCustomerId = null,
    string? StripeSubscriptionId = null,
    string? PlanId = null,
    string? SubscriptionStatus = null,
    DateTime? CurrentPeriodEnd = null,
    decimal? Amount = null,
    string? Currency = null,
    string? TransactionId = null);

/// <summary>
/// Payment provider abstraction. The development implementation runs without
/// credentials; the real one talks to the Stripe REST API (see 04-Monetarisierung.md).
/// </summary>
public interface IStripeService
{
    bool IsConfigured { get; }

    Task<CheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<CheckoutSession> CreateDonationCheckoutAsync(
        decimal amount, string currency, Guid? userId, string? email, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default);

    string CreateCustomerPortalUrl(string? stripeCustomerId, string returnUrl);

    bool VerifyWebhookSignature(string json, string? signatureHeader);

    StripeWebhookEvent? ParseWebhookEvent(string json);
}
