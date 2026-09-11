namespace BlogCms.Infrastructure.Media;

/// <summary>
/// Configuration for media handling, bound from the "Media" configuration section
/// (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Max upload size for article images (default 10 MB).</summary>
    public long ArticleMaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Max upload size for comment images (tighter, default 5 MB).</summary>
    public long CommentMaxBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Images are resized so that neither edge exceeds this value.</summary>
    public int MaxEdgePixels { get; set; } = 1600;

    /// <summary>Convert non-GIF images to WebP to reduce file size.</summary>
    public bool ConvertToWebP { get; set; } = true;

    /// <summary>"Local" (dev) or "S3" (S3-compatible object storage).</summary>
    public string Provider { get; set; } = "Local";

    // --- Local disk (dev) ---
    public string LocalRootPath { get; set; } = string.Empty;
    public string LocalPublicBaseUrl { get; set; } = "/uploads";

    // --- S3-compatible (e.g. Cloudflare R2, MinIO, Azure via S3 gateway) ---
    public string? S3ServiceUrl { get; set; }
    public string? S3Bucket { get; set; }
    public string? S3AccessKey { get; set; }
    public string? S3SecretKey { get; set; }

    /// <summary>Public/CDN base URL for served objects; falls back to the service URL.</summary>
    public string? S3PublicBaseUrl { get; set; }
}
