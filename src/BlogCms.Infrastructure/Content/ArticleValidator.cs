using BlogCms.Domain.Enums;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Fachliche Validierungsregeln des Beitrags-Workflows, geteilt von Anlegen und
/// Bearbeiten (FeatureFix1 BR-022 Titelbild-Quelle, BR-032 geplante Veröffentlichung).
/// </summary>
public static class ArticleValidator
{
    public static IReadOnlyList<PostError> Validate(ArticlePostRequest request, bool hasExistingImage)
    {
        var errors = new List<PostError>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add(new PostError(PostError.TitleField, "Titel ist erforderlich."));
        }

        // Ein Entwurf (Draft) darf unvollständig zwischengespeichert werden
        // (01-Content-Verwaltung.md). Erst für die Veröffentlichung oder Planung
        // sind Kurzbeschreibung, Inhalt und Titelbild-Quelle zwingend.
        var requiresFullContent = request.Status is ArticleStatus.Published or ArticleStatus.Scheduled;

        if (requiresFullContent)
        {
            if (string.IsNullOrWhiteSpace(request.Excerpt))
            {
                errors.Add(new PostError(
                    PostError.ExcerptField,
                    "Für die Veröffentlichung ist eine Kurzbeschreibung erforderlich (FeatureFix1 BR-023)."));
            }

            if (string.IsNullOrWhiteSpace(request.ContentMarkdown))
            {
                errors.Add(new PostError(
                    PostError.ContentMarkdownField,
                    "Für die Veröffentlichung ist ein Inhalt erforderlich (FeatureFix1 BR-024)."));
            }

            if (!request.HasTitleImageSource(hasExistingImage))
            {
                errors.Add(new PostError(
                    PostError.ImageUploadField,
                    "Bitte eine Titelbild-URL angeben oder ein Bild hochladen (FeatureFix1 BR-022)."));
            }
        }

        var scheduledUtc = NormalizeScheduledAtToUtc(request.ScheduledAt);
        if (request.Status == ArticleStatus.Scheduled &&
            (scheduledUtc is null || scheduledUtc <= DateTime.UtcNow))
        {
            errors.Add(new PostError(
                PostError.ScheduledAtField,
                "Für eine geplante Veröffentlichung ist ein zukünftiger Zeitpunkt erforderlich."));
        }

        return errors;
    }

    /// <summary>
    /// Löst den Zielstatus für die Aktion „Sofort veröffentlichen“: Liegt eine
    /// geplante Veröffentlichung mit Zukunftsdatum vor, bleibt es beim Planen,
    /// sonst wird sofort veröffentlicht.
    /// </summary>
    public static ArticleStatus ResolveTargetStatus(ArticleStatus selected, DateTime? scheduledAt)
    {
        var scheduledUtc = NormalizeScheduledAtToUtc(scheduledAt);
        if (selected != ArticleStatus.Scheduled)
        {
            return selected;
        }

        return scheduledUtc > DateTime.UtcNow ? ArticleStatus.Scheduled : ArticleStatus.Published;
    }

    /// <summary>
    /// Konvertiert einen aus <c>datetime-local</c> übergebenen Zeitpunkt sicher nach UTC.
    /// <c>DateTimeKind.Unspecified</c> wird als lokale Zeit der Server-/Blog-Zeitzone interpretiert.
    /// </summary>
    public static DateTime? NormalizeScheduledAtToUtc(DateTime? scheduledAt)
    {
        if (scheduledAt is null)
        {
            return null;
        }

        var value = scheduledAt.Value;
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}