using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A user comment on an article, optionally a reply via <see cref="ParentCommentId"/>
/// (see 02-Kommentarfunktion.md).
/// </summary>
public class Comment : EntityBase
{
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set for thread replies; one nesting level is recommended.</summary>
    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }

    public string ContentText { get; set; } = string.Empty;

    public CommentStatus Status { get; set; } = CommentStatus.Approved;

    /// <summary>Highlighted/featured by an admin or by the author of the article (FeatureFix1 BR-063).</summary>
    public bool IsHighlighted { get; set; }

    public DateTime? EditedAt { get; set; }

    /// <summary>Soft-delete marker; content is replaced by a placeholder to keep threads intact.</summary>
    public DateTime? DeletedAt { get; set; }

    // Relationships
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<VideoEmbed> VideoEmbeds { get; set; } = new List<VideoEmbed>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}
