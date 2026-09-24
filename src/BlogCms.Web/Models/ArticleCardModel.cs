using BlogCms.Domain.Entities;

namespace BlogCms.Web.Models;

/// <summary>
/// Parameter für das gemeinsame Akten-Karten-Partial (<c>_ArticleCard.cshtml</c>).
/// Bündelt den Artikel mit der bereits aufgelösten Titelbild-URL, damit das Partial
/// keine Kenntnis vom <c>IMediaService</c> benötigt.
/// </summary>
public sealed class ArticleCardModel
{
    public required Article Article { get; init; }

    public string? ImageUrl { get; init; }
}