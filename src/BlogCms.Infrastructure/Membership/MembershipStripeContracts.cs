namespace BlogCms.Infrastructure.Membership;

/// <summary>A created Stripe Checkout session.</summary>
public sealed record MembershipCheckoutSession(string Url, string Id);

/// <summary>Subset of a Stripe subscription needed by the membership feature.</summary>
public sealed record MembershipStripeSubscription(
    string SubscriptionId,
    string? CustomerId,
    string Status,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? PlanId = null);

/// <summary>
/// Verified Stripe webhook event plus the fields the membership feature needs.
/// The payload is reduced to a hash; no secrets or full customer data are stored.
/// </summary>
public sealed record MembershipStripeEvent(
    string EventId,
    string Type,
    Guid? UserId,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    string? SubscriptionStatus,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string PayloadHash,
    string? PlanId = null);

/// <summary>Stripe operations for the new membership feature.</summary>
public interface IStripeMembershipService
{
    bool IsConfigured { get; }

    Task<MembershipCheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<string> CreatePortalSessionAsync(
        string stripeCustomerId, string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies the signature and parses the event; null on invalid input.</summary>
    MembershipStripeEvent? VerifyAndParseWebhook(string json, string? signatureHeader);

    Task<MembershipStripeSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken = default);
}