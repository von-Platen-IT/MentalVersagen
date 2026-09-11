namespace BlogCms.Domain.Enums;

/// <summary>
/// Denormalized cache field on <c>User</c> for fast paywall checks.
/// Source of truth remains <c>Subscription.Status</c> (see DataSchema.md).
/// </summary>
public enum UserSubscriptionStatus
{
    None,
    Active,
    PastDue,
    Canceled
}
