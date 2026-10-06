using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Web.Content;

/// <summary>
/// Shared TempData success messages for the admin article pages, so create and edit
/// use identical wording.
/// </summary>
public static class ArticleMessages
{
    public static string ForCreate(Article article) => article.Status switch
    {
        ArticleStatus.Published =>
            $"Akte „{article.Title}“ wurde erfolgreich veröffentlicht und ist nun auf der Startseite sichtbar.",
        ArticleStatus.Scheduled =>
            $"Akte „{article.Title}“ wurde für die geplante Veröffentlichung am {article.ScheduledAt:dd.MM.yyyy HH:mm} gespeichert.",
        _ => $"Akte „{article.Title}“ wurde als Entwurf gespeichert."
    };

    public static string ForUpdate(Article article) =>
        $"Artikel „{article.Title}“ wurde aktualisiert.";
}