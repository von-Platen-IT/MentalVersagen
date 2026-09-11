namespace BlogCms.Domain.Enums;

/// <summary>
/// Moderation state of a comment (see 02-Kommentarfunktion.md).
/// </summary>
public enum CommentStatus
{
    Pending,
    Approved,
    Rejected,
    Flagged
}
