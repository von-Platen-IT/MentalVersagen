using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// Unified, append-only activity log. A single table records article views,
/// article creation, comment creation and article ratings. Event-specific
/// fields stay <c>null</c> for the other event types (see DataSchema.md).
/// </summary>
public class ActivityLogEntry : EntityBase
{
    public ActivityEventType EventType { get; set; }

    /// <summary>
    /// Client IP address (IPv4/IPv6) if available. This is personal data —
    /// see the retention note in DataSchema.md.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>Linked account, if the action was performed by a signed-in user.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Affected article (view, creation or rating).</summary>
    public Guid? ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Affected comment (comment creation).</summary>
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }

    /// <summary>Rating value, only set for <see cref="ActivityEventType.ArticleRated"/>.</summary>
    public RatingValue? RatingValue { get; set; }
}
