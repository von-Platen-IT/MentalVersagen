using BlogCms.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace BlogCms.Domain.Entities;

/// <summary>
/// Central account entity, extending ASP.NET Core Identity.
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>Public display name shown on articles and comments.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Authorization role. Premium access is additionally gated by subscription status.</summary>
    public UserRole Role { get; set; } = UserRole.Reader;

    /// <summary>
    /// Denormalized cache of the current subscription state for fast paywall checks.
    /// Source of truth is <see cref="Subscription.Status"/>.
    /// </summary>
    public UserSubscriptionStatus SubscriptionStatus { get; set; } = UserSubscriptionStatus.None;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft-delete marker / account deletion (personal data is anonymized).</summary>
    public DateTime? DeletedAt { get; set; }

    // Relationships
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}
