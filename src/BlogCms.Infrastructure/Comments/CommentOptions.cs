namespace BlogCms.Infrastructure.Comments;

/// <summary>Moderation strategy for newly created comments.</summary>
public enum CommentModerationMode
{
    /// <summary>Comments are visible immediately and can be reported/removed later.</summary>
    PostModeration,

    /// <summary>Comments start as Pending and require moderator approval.</summary>
    PreModeration
}

/// <summary>
/// Configuration for the comment feature, bound from the "Comments" configuration
/// section (see 02-Kommentarfunktion.md). Sensible defaults allow the app to run
/// without any explicit configuration.
/// </summary>
public sealed class CommentOptions
{
    public const string SectionName = "Comments";

    public CommentModerationMode ModerationMode { get; set; } = CommentModerationMode.PostModeration;

    public int MaxCommentsPerMinute { get; set; } = 5;

    public int EditWindowMinutes { get; set; } = 15;

    /// <summary>Blocked terms (spam patterns / profanity) that force Status = Flagged.</summary>
    public string[] Blocklist { get; set; } =
    [
        "viagra", "casino", "free-money", "http://spam", "idiot", "arschloch"
    ];
}
