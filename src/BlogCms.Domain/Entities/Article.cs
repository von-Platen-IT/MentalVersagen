using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A blog article written in Markdown (see 01-Content-Verwaltung.md).
/// </summary>
public class Article : EntityBase
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Unique URL segment.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Article body in Markdown, rendered + sanitized server-side.</summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>Optional teaser text, also used for the paywall preview.</summary>
    public string? Excerpt { get; set; }

    /// <summary>Mandatory editorial category; drives disclaimer/badge logic.</summary>
    public ArticleCategory Category { get; set; }

    /// <summary>When true, access requires an active subscription.</summary>
    public bool IsPremium { get; set; }

    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    public Guid AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>Set once when the article transitions to Published, never overwritten.</summary>
    public DateTime? PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft-delete marker.</summary>
    public DateTime? DeletedAt { get; set; }

    // Relationships
    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<VideoEmbed> VideoEmbeds { get; set; } = new List<VideoEmbed>();
}
