using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Web.Models.Api;

/// <summary>
/// Request zum Anlegen oder Bearbeiten eines Artikels über die Management-API
/// (Endpunkte <c>createArticle</c> und <c>updateArticle</c>). Bildet 1:1 auf den
/// Schreib-Workflow der Anwendungsschicht ab; die fachlichen Regeln (Pflichtfelder
/// bei Veröffentlichung, Titelbild-Quelle, geplante Veröffentlichung) greifen
/// identisch zur Web-Oberfläche.
/// </summary>
/// <remarks>
/// <para><b>Pflichtfelder:</b> <c>title</c>. Für <c>status = Published</c> oder
/// <c>Scheduled</c> zusätzlich: <c>excerpt</c>, <c>contentMarkdown</c> und eine
/// Titelbild-Quelle (<c>titleImageUrl</c>, <c>uploadedImageIds</c> oder ein beim
/// Bearbeiten bereits vorhandenes Bild).</para>
/// <para><b>Geplante Veröffentlichung:</b> Bei <c>status = Scheduled</c> muss
/// <c>scheduledAt</c> in der Zukunft liegen (ISO-8601, UTC).</para>
/// </remarks>
public sealed class ArticleWriteModel
{
    /// <summary>Titel des Artikels. Wird automatisch getrimmt.</summary>
    /// <example>Ein Blick hinter die Kulissen</example>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Eigener URL-Abschnitt. Leer lassen für automatische Generierung aus dem Titel.
    /// </summary>
    /// <example>ein-blick-hinter-die-kulissen</example>
    public string? Slug { get; set; }

    /// <summary>Kurzbeschreibung/Teaser. Bei Veröffentlichung Pflicht.</summary>
    /// <example>Was wirklich hinter den Schlagzeilen steckt.</example>
    public string? Excerpt { get; set; }

    /// <summary>Artikelinhalt in Markdown. Bei Veröffentlichung Pflicht.</summary>
    /// <example># Überschrift&#10;Der eigentliche Text …</example>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>
    /// Redaktionelle Kategorie. Erlaubte Werte: <c>Politik</c>, <c>Satire</c>,
    /// <c>Verschwoerungstheorien</c>.
    /// </summary>
    /// <example>Politik</example>
    public ArticleCategory Category { get; set; }

    /// <summary>
    /// Zugriffsstufe. Erlaubte Werte: <c>Public</c>, <c>Registered</c>, <c>Premium</c>.
    /// </summary>
    /// <example>Public</example>
    public ArticleAccessLevel AccessLevel { get; set; } = ArticleAccessLevel.Public;

    /// <summary>
    /// Workflow-Status. Erlaubte Werte: <c>Draft</c> (Entwurf), <c>Published</c>
    /// (sofort veröffentlichen), <c>Scheduled</c> (zeitgesteuert veröffentlichen —
    /// erfordert <c>scheduledAt</c>), <c>Archived</c>.
    /// </summary>
    /// <example>Published</example>
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    /// <summary>
    /// Geplanter Veröffentlichungszeitpunkt (ISO-8601, UTC). Nur relevant bei
    /// <c>status = Scheduled</c>; muss dann in der Zukunft liegen.
    /// </summary>
    /// <example>2026-12-24T18:00:00Z</example>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Externe URL des Titelbildes. Alternativ zu <c>uploadedImageIds</c> bzw. einem
    /// beim Bearbeiten bereits vorhandenen Bild.
    /// </summary>
    /// <example>https://example.com/titelbild.jpg</example>
    public string? TitleImageUrl { get; set; }

    /// <summary>
    /// URL eines einzubettenden Videos (YouTube, Vimeo etc.). Leer lassen, um ein
    /// bestehendes Video zu entfernen.
    /// </summary>
    /// <example>https://www.youtube.com/watch?v=dQw4w9WgXcQ</example>
    public string? VideoUrl { get; set; }

    /// <summary>Schlagwörter (Tags) für die Filterung.</summary>
    /// <example>["Innenpolitik", "Analyse"]</example>
    public List<string> Tags { get; set; } = [];

    /// <summary>Hashtags (ohne führendes #) für die Themensuche.</summary>
    /// <example>["DeepDive", "Faktencheck"]</example>
    public List<string> Hashtags { get; set; } = [];

    /// <summary>
    /// IDs zuvor hochgeladener Bilder, die dem Artikel zugeordnet werden sollen.
    /// </summary>
    public List<Guid> UploadedImageIds { get; set; } = [];
}

/// <summary>
/// Vollständige Darstellung eines Artikels in API-Antworten
/// (Endpunkte <c>getArticle</c>, <c>listArticles</c>, <c>createArticle</c>,
/// <c>updateArticle</c>).
/// </summary>
public sealed class ArticleReadModel
{
    /// <summary>Eindeutige ID (UUID) — stabil als Referenz nutzbar.</summary>
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>URL-Abschnitt; ergibt zusammen mit der Basis-URL die öffentliche Adresse.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Excerpt { get; set; }

    /// <summary>Artikelinhalt in Markdown (Quelltext, nicht gerendert).</summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    public ArticleCategory Category { get; set; }
    public ArticleAccessLevel AccessLevel { get; set; }
    public ArticleStatus Status { get; set; }

    /// <summary>Geplanter Veröffentlichungszeitpunkt (ISO-8601, UTC), sonst null.</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>Tatsächlicher Veröffentlichungszeitpunkt (ISO-8601, UTC), sonst null.</summary>
    public DateTime? PublishedAt { get; set; }

    public string? TitleImageUrl { get; set; }

    /// <summary>URL des eingebetteten Videos, sofern vorhanden.</summary>
    public string? VideoUrl { get; set; }

    public List<string> Tags { get; set; } = [];
    public List<string> Hashtags { get; set; } = [];

    /// <summary>IDs der dem Artikel zugeordneten Medien (Bilder).</summary>
    public List<Guid> MediaIds { get; set; } = [];

    /// <summary>UUID des Autoren (bei API-Anlage der konfigurierte API-Benutzer).</summary>
    public Guid AuthorId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Erzeugt das Lesemodell aus einer geladenen Artikel-Entität.</summary>
    public static ArticleReadModel FromEntity(Article article) => new()
    {
        Id = article.Id,
        Title = article.Title,
        Slug = article.Slug,
        Excerpt = article.Excerpt,
        ContentMarkdown = article.ContentMarkdown,
        Category = article.Category,
        AccessLevel = article.AccessLevel,
        Status = article.Status,
        ScheduledAt = article.ScheduledAt,
        PublishedAt = article.PublishedAt,
        TitleImageUrl = article.TitleImageUrl,
        VideoUrl = article.VideoEmbeds.Select(v => v.OriginalUrl).FirstOrDefault(),
        Tags = article.ArticleTags
            .Where(at => at.Tag is not null)
            .Select(at => at.Tag!.Name)
            .ToList(),
        Hashtags = article.ArticleHashtags
            .Where(ah => ah.Hashtag is not null)
            .Select(ah => ah.Hashtag!.Name)
            .ToList(),
        MediaIds = article.MediaAssets.Select(m => m.Id).ToList(),
        AuthorId = article.AuthorId,
        CreatedAt = article.CreatedAt,
        UpdatedAt = article.UpdatedAt
    };
}

/// <summary>
/// Paginierte Listenantwort der Management-API — analog zum Raster der
/// öffentlichen Artikelübersicht.
/// </summary>
/// <param name="Items">Elemente der aktuellen Seite.</param>
/// <param name="Total">Gesamtzahl passender Elemente.</param>
/// <param name="Page">Aktuelle Seite (1-basiert).</param>
/// <param name="PageSize">Elemente pro Seite.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
