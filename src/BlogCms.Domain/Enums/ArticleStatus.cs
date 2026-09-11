namespace BlogCms.Domain.Enums;

/// <summary>
/// Publication workflow state. Only <c>Published</c> articles are publicly visible.
/// </summary>
public enum ArticleStatus
{
    Draft,
    Published,
    Archived
}
