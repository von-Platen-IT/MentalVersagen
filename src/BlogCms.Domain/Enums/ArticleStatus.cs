namespace BlogCms.Domain.Enums;

/// <summary>
/// Publication workflow state (FeatureFix1 BR-030/BR-031/BR-032). Only <c>Published</c>
/// articles are publicly visible. <c>Scheduled</c> articles become <c>Published</c>
/// automatically once their <c>ScheduledAt</c> time is reached.
/// </summary>
public enum ArticleStatus
{
    Draft,
    Scheduled,
    Published,
    Archived
}
