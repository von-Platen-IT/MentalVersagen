using System.ComponentModel.DataAnnotations.Schema;
using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A 👍/👎 rating for an article or a comment (FeatureFix1 BR-070/BR-071/BR-072).
/// Exactly one of <see cref="ArticleId"/> or <see cref="CommentId"/> is set.
/// A user may hold at most one rating per target; changing it updates this row.
/// </summary>
public class Rating : EntityBase
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set for article ratings.</summary>
    public Guid? ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Set for comment (and reply) ratings.</summary>
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public RatingValue Value { get; set; }

    /// <summary>Timestamp of the last change to the rating (nullable while unchanged).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Convenience: +1 for a thumb up, -1 for a thumb down.</summary>
    [NotMapped]
    public int Score => Value == RatingValue.ThumbUp ? 1 : -1;
}
