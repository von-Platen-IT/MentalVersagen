namespace BlogCms.Domain.Enums;

/// <summary>
/// Kind of recorded activity in the unified activity log (see DataSchema.md).
/// Stored as a readable string in the database.
/// </summary>
public enum ActivityEventType
{
    /// <summary>A published article page was opened.</summary>
    ArticleViewed,

    /// <summary>An article was created.</summary>
    ArticleCreated,

    /// <summary>A comment was created.</summary>
    CommentCreated,

    /// <summary>An article received a thumbs up/down.</summary>
    ArticleRated
}
