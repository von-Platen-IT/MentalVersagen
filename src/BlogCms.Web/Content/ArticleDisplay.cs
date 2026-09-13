using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Media;

namespace BlogCms.Web.Content;

/// <summary>Shared presentation helpers for articles (title image, access badge).</summary>
public static class ArticleDisplay
{
    /// <summary>
    /// Resolves the title image (FeatureFix1 BR-022): an external URL wins, otherwise
    /// the first uploaded article image is used. Returns <c>null</c> when neither exists.
    /// </summary>
    public static string? ResolveTitleImage(Article article, IMediaService media)
    {
        if (!string.IsNullOrWhiteSpace(article.TitleImageUrl))
        {
            return article.TitleImageUrl;
        }

        var uploaded = article.MediaAssets
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault();

        return uploaded is null ? null : media.GetUrl(uploaded);
    }

    public static string AccessLabel(ArticleAccessLevel level) => level switch
    {
        ArticleAccessLevel.Registered => "Nur angemeldet",
        ArticleAccessLevel.Premium => "Premium",
        _ => "Öffentlich"
    };

    public static string AccessBadgeClass(ArticleAccessLevel level) => level switch
    {
        ArticleAccessLevel.Registered => "bg-info text-dark",
        ArticleAccessLevel.Premium => "bg-dark",
        _ => "bg-success"
    };
}
