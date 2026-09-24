using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages;

/// <summary>
/// Comfortable search page: full-text search, date range, hashtag and sorting.
/// All filters are plain GET parameters, so results are shareable and work
/// without JavaScript.
/// </summary>
public class SucheModel : PageModel
{
    private const int PageSize = 10;

    private readonly IArticleService _articles;
    private readonly IMediaService _media;

    public SucheModel(IArticleService articles, IMediaService media)
    {
        _articles = articles;
        _media = media;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public IReadOnlyList<HashtagUsage> AllHashtags { get; private set; } = [];

    public IReadOnlyList<Tag> AllTags { get; private set; } = [];

    public string? Query { get; private set; }

    public ArticleCategory? Category { get; private set; }

    public string? TagSlug { get; private set; }

    public string? HashtagSlug { get; private set; }

    public DateTime? DateFrom { get; private set; }

    public DateTime? DateTo { get; private set; }

    public ArticleSortOrder Sort { get; private set; } = ArticleSortOrder.Newest;

    public int PageNumber { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    /// <summary>True when at least one filter is active (for the result heading).</summary>
    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(Query) ||
        Category is not null ||
        !string.IsNullOrWhiteSpace(TagSlug) ||
        !string.IsNullOrWhiteSpace(HashtagSlug) ||
        DateFrom is not null ||
        DateTo is not null;

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync(
        string? query = null,
        ArticleCategory? category = null,
        string? tag = null,
        string? hashtag = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        ArticleSortOrder sort = ArticleSortOrder.Newest,
        int pageNumber = 1)
    {
        Query = query;
        Category = category;
        TagSlug = tag;
        HashtagSlug = hashtag;
        DateFrom = dateFrom;
        DateTo = dateTo;
        Sort = sort;
        PageNumber = Math.Max(1, pageNumber);

        var articleQuery = new ArticleQuery(
            Page: PageNumber,
            PageSize: PageSize,
            Category: category,
            TagSlug: string.IsNullOrWhiteSpace(tag) ? null : tag,
            HashtagSlug: string.IsNullOrWhiteSpace(hashtag) ? null : hashtag,
            Search: string.IsNullOrWhiteSpace(query) ? null : query,
            Sort: sort,
            DateFrom: dateFrom,
            DateTo: dateTo);

        var (items, total) = await _articles.SearchPublishedAsync(articleQuery);
        Articles = items;
        TotalCount = total;
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

        AllHashtags = await _articles.GetPublishedHashtagsAsync();
        AllTags = await _articles.GetAllTagsAsync();
    }
}
