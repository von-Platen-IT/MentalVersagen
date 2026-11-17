using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Settings;
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
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;

    public ArticlesController(
        IArticleService articles,
        IMediaService media,
        ICategoryService categories,
        ISettingsService settings)
    {
        _articles = articles;
        _media = media;
        _categories = categories;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? hashtag = null,
        string? category = null,
        string? categories = null,
        string? hashtags = null,
        string? match = null,
        string? query = null,
        string? sort = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        var sortOrder = Enum.TryParse<ArticleSortOrder>(sort, ignoreCase: true, out var parsed)
            ? parsed
            : ArticleSortOrder.Newest;

        var filterMatch = Enum.TryParse<ArticleFilterMatch>(match, ignoreCase: true, out var parsedMatch)
            ? parsedMatch
            : ArticleFilterMatch.Any;

        // "category"/"hashtag" remain supported as single-value aliases; the
        // plural parameters accept comma-separated lists for bookmarkable URLs.
        var categorySlugs = ParseList(categories);
        if (!string.IsNullOrWhiteSpace(category))
        {
            categorySlugs.Insert(0, category);
        }

        var categoryIds = new List<Guid>();
        foreach (var slug in categorySlugs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = await _categories.GetBySlugAsync(slug, cancellationToken);
            if (found is not null)
            {
                categoryIds.Add(found.Id);
            }
        }

        var hashtagSlugs = ParseList(hashtags);
        if (!string.IsNullOrWhiteSpace(hashtag))
        {
            hashtagSlugs.Insert(0, hashtag);
        }

        var effectivePageSize = pageSize > 0
            ? pageSize
            : await _settings.GetArticlesPerPageAsync(cancellationToken);

        var articleQuery = new ArticleQuery(
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(effectivePageSize, 1, MaxPageSize),
            Search: string.IsNullOrWhiteSpace(query) ? null : query,
            Sort: sortOrder,
            DateFrom: dateFrom,
            DateTo: dateTo,
            CategoryIds: categoryIds.Count > 0 ? categoryIds : null,
            HashtagSlugs: hashtagSlugs.Count > 0 ? hashtagSlugs : null,
            Match: filterMatch);

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
                category = CategoryDisplay.Slug(a.Category),
                categoryLabel = CategoryDisplay.Name(a.Category),
                categoryColor = CategoryDisplay.Color(a.Category),
                origin = a.Origin.ToString(),
                originDescription = OriginDisplay.Description(a.Origin),
                archiveCode = a.Id.ToString("N")[..8].ToUpperInvariant(),
                publishedAt = a.PublishedAt?.ToString("dd.MM.yyyy"),
                author = a.Author?.DisplayName ?? "Archiv",
                imageUrl = ArticleDisplay.ResolveTitleImage(a, _media),
                accessLabel = ArticleDisplay.AccessLabel(a.AccessLevel),
                views = a.ViewCount,
                likes = a.Ratings.Count(r => r.Value == RatingValue.ThumbUp),
                thumbsDown = a.Ratings.Count(r => r.Value == RatingValue.ThumbDown)
            })
        });
    }

    private static List<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
