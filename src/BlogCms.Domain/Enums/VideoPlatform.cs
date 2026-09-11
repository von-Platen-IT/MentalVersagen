namespace BlogCms.Domain.Enums;

/// <summary>
/// External video platform detected from an input URL (see 03-Medien-Upload-und-Embedding.md).
/// Unknown patterns are marked as <c>Other</c> and rendered as a plain link.
/// </summary>
public enum VideoPlatform
{
    YouTube,
    Vimeo,
    X,
    TikTok,
    Other
}
