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

        if (!request.HasTitleImageSource(hasExistingImage))
        {
            errors.Add(new PostError(
                PostError.ImageUploadField,
                "Bitte eine Titelbild-URL angeben oder ein Bild hochladen (FeatureFix1 BR-022)."));
        }

        if (request.Status == ArticleStatus.Scheduled &&
            (request.ScheduledAt is null || request.ScheduledAt <= DateTime.UtcNow))
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
        if (selected != ArticleStatus.Scheduled)
        {
            return selected;
        }

        return scheduledAt > DateTime.UtcNow ? ArticleStatus.Scheduled : ArticleStatus.Published;
    }
}