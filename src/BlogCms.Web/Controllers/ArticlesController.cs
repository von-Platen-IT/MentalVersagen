using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// REST endpoint for the article grid. The landing page uses it to re-render the
/// grid without a page reload when a hashtag is picked or the sort order changes.
/// </summary>
[Route("api/articles")]
public class ArticlesController : Controller
{
    private const int MaxPageSize = 24;

    private readonly IArticleService _articles;
    private readonly IMediaService _media;

    public ArticlesController(IArticleService articles, IMediaService media)
    {
        _articles = articles;
        _media = media;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? hashtag = null,
        string? query = null,
        string? sort = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        var sortOrder = Enum.TryParse<ArticleSortOrder>(sort, ignoreCase: true, out var parsed)
            ? parsed
            : ArticleSortOrder.Newest;

        var articleQuery = new ArticleQuery(
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(pageSize, 1, MaxPageSize),
            HashtagSlug: string.IsNullOrWhiteSpace(hashtag) ? null : hashtag,
            Search: string.IsNullOrWhiteSpace(query) ? null : query,
            Sort: sortOrder,
            DateFrom: dateFrom,
            DateTo: dateTo);

        var (items, total) = await _articles.SearchPublishedAsync(articleQuery, cancellationToken);

        return Json(new
        {
            total,
            page = articleQuery.Page,
            pageSize = articleQuery.PageSize,
            items = items.Select(a => new
            {
                id = a.Id,
                slug = a.Slug,
                title = a.Title,
                excerpt = a.Excerpt,
                category = a.Category.ToString(),
                categoryLabel = CategoryDisplay.Label(a.Category),
                archiveCode = a.Id.ToString("N")[..8].ToUpperInvariant(),
                publishedAt = a.PublishedAt?.ToString("dd.MM.yyyy"),
                author = a.Author?.DisplayName ?? "Archiv",
                imageUrl = ArticleDisplay.ResolveTitleImage(a, _media),
                accessLabel = ArticleDisplay.AccessLabel(a.AccessLevel)
            })
        });
    }
}
