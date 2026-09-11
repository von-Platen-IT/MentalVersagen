using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// An uploaded image, attachable to articles or comments. Binary content lives in
/// object storage; <see cref="StoragePath"/> is the storage key (see 03-...).
/// </summary>
public class MediaAsset : EntityBase
{
    /// <summary>Polymorphic discriminator.</summary>
    public MediaOwnerType OwnerType { get; set; }

    /// <summary>Set when <see cref="OwnerType"/> is <see cref="MediaOwnerType.Article"/>.</summary>
    public Guid? ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Set when <see cref="OwnerType"/> is <see cref="MediaOwnerType.Comment"/>.</summary>
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public Guid UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }

    /// <summary>Path/key in the object storage (e.g. S3/R2/Blob).</summary>
    public string StoragePath { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>Accessibility alternative text.</summary>
    public string? AltText { get; set; }
}
