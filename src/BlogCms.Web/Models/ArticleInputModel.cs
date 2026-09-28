using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BlogCms.Web.Models;

/// <summary>Form model shared by the admin article create and edit pages.</summary>
public class ArticleInputModel
{
    [Required(ErrorMessage = "Titel ist erforderlich.")]
    [StringLength(300)]
    [Display(Name = "Titel")]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Slug (optional, wird sonst aus dem Titel erzeugt)")]
    public string? Slug { get; set; }

    [Url(ErrorMessage = "Bitte eine gültige URL angeben.")]
    [StringLength(500)]
    [Display(Name = "Titelbild-URL (extern)")]
    public string? TitleImageUrl { get; set; }

    [Required(ErrorMessage = "Kurzbeschreibung ist erforderlich.")]
    [StringLength(500)]
    [Display(Name = "Kurzbeschreibung / Teaser")]
    public string? Excerpt { get; set; }

    [Required(ErrorMessage = "Kategorie ist Pflichtfeld.")]
    [Display(Name = "Kategorie")]
    public ArticleCategory Category { get; set; }

    [Display(Name = "Themengebiet")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "Zugriffsstufe")]
    public ArticleAccessLevel AccessLevel { get; set; } = ArticleAccessLevel.Public;

    [Display(Name = "Status")]
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    [Display(Name = "Geplante Veröffentlichung (bei Status „Scheduled”)")]
    [DataType(DataType.DateTime)]
    public DateTime? ScheduledAt { get; set; }

    [Display(Name = "Tags (kommagetrennt)")]
    public string? Tags { get; set; }

    [Display(Name = "Hashtags (kommagetrennt, mit oder ohne #)")]
    public string? Hashtags { get; set; }

    [Required(ErrorMessage = "Inhalt ist erforderlich.")]
    [Display(Name = "Inhalt (Markdown)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    [Display(Name = "Video-URL (YouTube, Vimeo, X, TikTok)")]
    public string? VideoUrl { get; set; }

    [Display(Name = "Titelbild hochladen")]
    public IFormFile? ImageUpload { get; set; }

    [Display(Name = "Weitere Bilder hochladen (im Text einbaubar)")]
    public List<IFormFile> ContentImageUploads { get; set; } = [];

    /// <summary>
    /// Comma-separated ids of images uploaded through the Markdown editor before the
    /// article existed. They are attached to the article on save (see
    /// IMediaService.AttachToArticleAsync).
    /// </summary>
    public string? UploadedImageIds { get; set; }

    /// <summary>
    /// JSON list of images uploaded through the editor (<c>[{id,url,alt}]</c>), used
    /// by the create page to render the click-to-insert gallery. Round-trips through
    /// the form so the gallery survives a validation error.
    /// </summary>
    public string? UploadedImagesJson { get; set; }

    /// <summary>Parses the comma-separated <see cref="UploadedImageIds"/> into GUIDs.</summary>
    public IEnumerable<Guid> ParseUploadedImageIds()
    {
        if (string.IsNullOrWhiteSpace(UploadedImageIds))
        {
            return [];
        }

        return UploadedImageIds
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct();
    }

    /// <summary>
    /// Validates that a title image source is present: either an external URL, a new
    /// upload, or (on edit) an already stored image.
    /// </summary>
    public bool HasTitleImageSource(bool hasExistingImage)
    {
        return !string.IsNullOrWhiteSpace(TitleImageUrl)
            || ImageUpload is { Length: > 0 }
            || hasExistingImage
            || ParseUploadedImageIds().Any();
    }

    public static IEnumerable<string> ParseTags(string? value) => Parse(value);

    public static IEnumerable<string> ParseHashtags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.TrimStart('#'))
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
