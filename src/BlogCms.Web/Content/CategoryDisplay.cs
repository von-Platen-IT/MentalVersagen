using BlogCms.Domain.Enums;

namespace BlogCms.Web.Content;

/// <summary>
/// Presentation helpers for article categories: readable label, Bootstrap badge
/// class and the disclaimer text shown for sensitive categories
/// (see 01-Content-Verwaltung.md).
/// </summary>
public static class CategoryDisplay
{
    public static string Label(ArticleCategory category) => category switch
    {
        ArticleCategory.Politik => "Politik",
        ArticleCategory.Satire => "Satire",
        ArticleCategory.Verschwoerungstheorien => "Verschwörungstheorien",
        _ => category.ToString()
    };

    public static string BadgeClass(ArticleCategory category) => category switch
    {
        ArticleCategory.Politik => "bg-primary",
        ArticleCategory.Satire => "bg-warning text-dark",
        ArticleCategory.Verschwoerungstheorien => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>Returns the mandated hint text for a category, or null if none is needed.</summary>
    public static string? Disclaimer(ArticleCategory category) => category switch
    {
        ArticleCategory.Satire =>
            "Dieser Beitrag ist Satire. Inhalt und Aussagen sind überspitzt und nicht wörtlich zu nehmen.",
        ArticleCategory.Verschwoerungstheorien =>
            "Dieser Beitrag behandelt verschwörungstheoretische Inhalte zur kritischen Auseinandersetzung " +
            "und dient nicht der Verbreitung von Falschinformationen.",
        _ => null
    };
}
