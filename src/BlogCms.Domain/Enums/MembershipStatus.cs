namespace BlogCms.Domain.Enums;

/// <summary>
/// Local mirror of the Stripe subscription state for the new membership feature.
/// </summary>
public enum MembershipStatus
{
    None,
    Incomplete,
    Trialing,
    Active,
    PastDue,
    Canceled,
    Unpaid
}