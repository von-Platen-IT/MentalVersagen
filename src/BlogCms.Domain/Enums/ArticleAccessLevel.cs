namespace BlogCms.Domain.Enums;

/// <summary>
/// Access level of an article (FeatureFix1 BR-110/BR-113).
/// Determines who may read the actual content:
/// <list type="bullet">
/// <item><see cref="Public"/> — everyone (including anonymous visitors).</item>
/// <item><see cref="Registered"/> — authenticated users only.</item>
/// <item><see cref="Premium"/> — users with an active entitlement (subscription) or admins.</item>
/// </list>
/// </summary>
public enum ArticleAccessLevel
{
    Public,
    Registered,
    Premium
}
