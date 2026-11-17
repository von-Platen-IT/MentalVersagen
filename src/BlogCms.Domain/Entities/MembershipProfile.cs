using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// Membership state of a user for the new Stripe membership feature. Intentionally
/// separate from <see cref="Subscription"/> so the existing payment flow remains
/// untouched.
/// </summary>
public class MembershipProfile : EntityBase
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    /// <summary>Stripe price ID of the current plan (monthly/yearly), for the admin overview.</summary>
    public string? PlanId { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.None;

    /// <summary>End of the current billing period (UTC).</summary>
    public DateTime? CurrentPeriodEnd { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>Admin-granted access without a paid subscription.</summary>
    public bool IsComplimentary { get; set; }

    /// <summary>Expiry of the complimentary access; <c>null</c> means unlimited.</summary>
    public DateTime? ComplimentaryUntil { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}