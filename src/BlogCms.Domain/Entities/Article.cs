using System.ComponentModel.DataAnnotations.Schema;
using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A blog article written in Markdown (see 01-Content-Verwaltung.md).
/// </summary>
public class Article : EntityBase
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Unique URL segment. Published URLs stay stable across title edits.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// External title image URL (FeatureFix1 BR-022). When empty, the first uploaded
    /// <see cref="MediaAsset"/> serves as the title image.
    /// </summary>
    public string? TitleImageUrl { get; set; }

    /// <summary>Article body in Markdown, rendered + sanitized server-side.</summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>Teaser/intro, also used for list previews and the paywall preview (mandatory in the UI).</summary>
    public string? Excerpt { get; set; }

    /// <summary>Mandatory editorial category; drives disclaimer/badge logic.</summary>
    public ArticleCategory Category { get; set; }

    /// <summary>Access level (public / registered / premium) — FeatureFix1 BR-110/BR-113.</summary>
    public ArticleAccessLevel AccessLevel { get; set; } = ArticleAccessLevel.Public;

    /// <summary>
    /// Convenience, not persisted: <c>true</c> when <see cref="AccessLevel"/> is
    /// <see cref="ArticleAccessLevel.Premium"/>. Kept for backward compatibility.
    /// </summary>
    [NotMapped]
    public bool IsPremium
    {
        get => AccessLevel == ArticleAccessLevel.Premium;
        set => AccessLevel = value ? ArticleAccessLevel.Premium : ArticleAccessLevel.Public;
    }

    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    /// <summary>Planned publication time; required while <see cref="Status"/> is Scheduled.</summary>
    public DateTime? ScheduledAt { get; set; }

    public Guid AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>Set once when the article transitions to Published, never overwritten.</summary>
    public DateTime? PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft-delete marker.</summary>
    public DateTime? DeletedAt { get; set; }

    // Relationships
    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
    public ICollection<ArticleHashtag> ArticleHashtags { get; set; } = new List<ArticleHashtag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<VideoEmbed> VideoEmbeds { get; set; } = new List<VideoEmbed>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public ICollection<LinkListItem> LinkListItems { get; set; } = new List<LinkListItem>();
}
