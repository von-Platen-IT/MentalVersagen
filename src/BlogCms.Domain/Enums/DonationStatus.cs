namespace BlogCms.Domain.Enums;

/// <summary>
/// Payment state of a one-time donation.
/// </summary>
public enum DonationStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}
