namespace BlogCms.Domain.Enums;

/// <summary>
/// Status of a <c>Subscription</c>, mirrored from Stripe webhooks.
/// </summary>
public enum SubscriptionStatus
{
    Active,
    PastDue,
    Canceled,
    Trialing
}
