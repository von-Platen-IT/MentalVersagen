namespace BlogCms.Domain.Entities;

/// <summary>
/// A newsletter recipient managed via double opt-in (see 04-Monetarisierung.md).
/// </summary>
public class NewsletterSubscriber : EntityBase
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Linked registered user, if any.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set when the double opt-in confirmation link is clicked.</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Set on unsubscribe (no hard delete, to handle re-signup cleanly).</summary>
    public DateTime? UnsubscribedAt { get; set; }
}
