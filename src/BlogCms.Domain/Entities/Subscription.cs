using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A Stripe subscription; the source of truth for billing state
/// (see 04-Monetarisierung.md).
/// </summary>
public class Subscription : EntityBase
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string StripeCustomerId { get; set; } = string.Empty;

    public string StripeSubscriptionId { get; set; } = string.Empty;

    /// <summary>References the Stripe price/product; plan data is not duplicated locally.</summary>
    public string PlanId { get; set; } = string.Empty;

    /// <summary>Mirrored from Stripe webhooks.</summary>
    public SubscriptionStatus Status { get; set; }

    public DateTime CurrentPeriodEnd { get; set; }

    public DateTime? CanceledAt { get; set; }
}
