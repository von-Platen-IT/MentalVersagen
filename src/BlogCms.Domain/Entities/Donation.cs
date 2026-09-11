using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A one-time payment, independent of subscriptions. Anonymous donations are
/// allowed, so <see cref="UserId"/> is nullable (see 04-Monetarisierung.md).
/// </summary>
public class Donation : EntityBase
{
    /// <summary>Nullable: anonymous donations are supported.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO currency code, e.g. <c>EUR</c>.</summary>
    public string Currency { get; set; } = "EUR";

    public PaymentProvider Provider { get; set; }

    public string ProviderTransactionId { get; set; } = string.Empty;

    public DonationStatus Status { get; set; } = DonationStatus.Pending;
}
