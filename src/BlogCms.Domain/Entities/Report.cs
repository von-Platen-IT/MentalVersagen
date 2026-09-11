using BlogCms.Domain.Enums;

namespace BlogCms.Domain.Entities;

/// <summary>
/// A user-submitted report against a comment (see 02-Kommentarfunktion.md).
/// </summary>
public class Report : EntityBase
{
    public Guid CommentId { get; set; }
    public Comment? Comment { get; set; }

    public Guid ReportedByUserId { get; set; }
    public User? ReportedByUser { get; set; }

    public ReportReason Reason { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Open;
}
