using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
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

    /// <summary>
    /// Maps the form data onto the application-layer request. Pure mapping — all
    /// business rules (validation, target status, persistence) live in
    /// IArticlePostingService.
    /// </summary>
    public ArticlePostRequest ToRequest(ArticleStatus requestedStatus)
    {
        return new ArticlePostRequest(
            Title,
            Slug,
            TitleImageUrl,
            Excerpt ?? string.Empty,
            ContentMarkdown,
            Category,
            AccessLevel,
            requestedStatus,
            ScheduledAt,
            ArticlePostRequest.ParseTags(Tags),
            ArticlePostRequest.ParseHashtags(Hashtags),
            VideoUrl,
            ImageUpload is { Length: > 0 }
                ? new ArticleUpload(ImageUpload.OpenReadStream(), ImageUpload.FileName)
                : null,
            ContentImageUploads
                .Where(f => f is { Length: > 0 })
                .Select(f => new ArticleUpload(f.OpenReadStream(), f.FileName))
                .ToList(),
            ArticlePostRequest.ParseUploadedImageIds(UploadedImageIds));
    }
}
