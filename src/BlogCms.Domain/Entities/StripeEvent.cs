namespace BlogCms.Domain.Entities;

/// <summary>
/// Idempotency record for processed Stripe webhook events. The raw payload is not
/// stored; only a hash is kept for diagnostics.
/// </summary>
public class StripeEvent : EntityBase
{
    public string StripeEventId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 hash of the raw payload; no customer data or secrets.</summary>
    public string? PayloadHash { get; set; }
}