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

    [Required(ErrorMessage = "Inhalt ist erforderlich.")]
    [Display(Name = "Inhalt (Markdown)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Teaser / Excerpt (optional)")]
    public string? Excerpt { get; set; }

    [Required(ErrorMessage = "Kategorie ist Pflichtfeld.")]
    [Display(Name = "Kategorie")]
    public ArticleCategory Category { get; set; }

    [Display(Name = "Premium-Artikel (Paywall)")]
    public bool IsPremium { get; set; }

    [Display(Name = "Status")]
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    [Display(Name = "Tags (kommagetrennt)")]
    public string? Tags { get; set; }

    [Display(Name = "Video-URL (YouTube, Vimeo, X, TikTok)")]
    public string? VideoUrl { get; set; }

    [Display(Name = "Bild hochladen")]
    public IFormFile? ImageUpload { get; set; }

    public static IEnumerable<string> ParseTags(string? value)
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
