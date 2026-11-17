using BlogCms.Domain.Enums;

namespace BlogCms.Web.Content;

/// <summary>
/// Presentation helpers for the editorial origin (human / AI / hybrid). The label
/// is short (for badges), the description is the hover/aria text.
/// </summary>
public static class OriginDisplay
{
    public static string Label(ArticleOrigin origin) => origin switch
    {
        ArticleOrigin.Ai => "KI",
        ArticleOrigin.Hybrid => "Hybrid",
        _ => "Mensch"
    };

    public static string Description(ArticleOrigin origin) => origin switch
    {
        ArticleOrigin.Ai => "Mit künstlicher Intelligenz erstellt",
        ArticleOrigin.Hybrid => "Mensch und KI (Hybrid)",
        _ => "Von einem Menschen erstellt"
    };

    /// <summary>CSS modifier suffix, e.g. <c>ai</c>, <c>hybrid</c>, <c>human</c>.</summary>
    public static string Modifier(ArticleOrigin origin) =>
        origin.ToString().ToLowerInvariant();
}
