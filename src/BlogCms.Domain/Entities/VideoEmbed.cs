using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A link to an external video (no self-hosting). Embed code is obtained via
/// oEmbed and cached (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public class VideoEmbed : EntityBase
{
    /// <summary>Polymorphic discriminator.</summary>
    public MediaOwnerType OwnerType { get; set; }

    /// <summary>Set when <see cref="OwnerType"/> is <see cref="MediaOwnerType.Article"/>.</summary>
    public Guid? ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Set when <see cref="OwnerType"/> is <see cref="MediaOwnerType.Comment"/>.</summary>
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public VideoPlatform Platform { get; set; } = VideoPlatform.Other;

    /// <summary>URL entered by the user.</summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>Embed HTML returned by the platform's oEmbed endpoint.</summary>
    public string EmbedHtml { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }
}
