using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Settings;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages;

/// <summary>
/// Comfortable search page: full-text search, date range, multiple categories,
/// multiple hashtags and sorting. All filters are plain GET parameters, so
/// results are shareable and work without JavaScript.
/// </summary>
public class SucheModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;

    public SucheModel(
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

    /// <summary>Admin-configured number of articles shown per list.</summary>
    public int ArticlesPerPage { get; private set; } = SettingKeys.DefaultArticlesPerPage;

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public IReadOnlyList<HashtagUsage> AllHashtags { get; private set; } = [];

    public IReadOnlyList<Tag> AllTags { get; private set; } = [];

    public IReadOnlyList<Category> AllCategories { get; private set; } = [];

    public string? Query { get; private set; }

    /// <summary>Selected category slugs (multi-select, bookmarkable).</summary>
    public IReadOnlyList<string> CategorySlugs { get; private set; } = [];

    public string? TagSlug { get; private set; }

    /// <summary>Selected hashtag slugs (multi-select, bookmarkable).</summary>
    public IReadOnlyList<string> HashtagSlugs { get; private set; } = [];

    public ArticleFilterMatch Match { get; private set; } = ArticleFilterMatch.Any;

    public DateTime? DateFrom { get; private set; }

    public DateTime? DateTo { get; private set; }

    public ArticleSortOrder Sort { get; private set; } = ArticleSortOrder.Newest;

    public int PageNumber { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    /// <summary>True when at least one filter is active (for the result heading).</summary>
    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(Query) ||
        CategorySlugs.Count > 0 ||
        !string.IsNullOrWhiteSpace(TagSlug) ||
        HashtagSlugs.Count > 0 ||
        DateFrom is not null ||
        DateTo is not null;

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync(
        string? query = null,
        string? category = null,
        string? categories = null,
        string? tag = null,
        string? hashtag = null,
        string? hashtags = null,
        string? match = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        ArticleSortOrder sort = ArticleSortOrder.Newest,
        int pageNumber = 1)
    {
        Query = query;
        CategorySlugs = Merge(categories, category);
        HashtagSlugs = Merge(hashtags, hashtag);
        TagSlug = tag;
        Match = Enum.TryParse<ArticleFilterMatch>(match, ignoreCase: true, out var parsedMatch)
            ? parsedMatch
            : ArticleFilterMatch.Any;
        DateFrom = dateFrom;
        DateTo = dateTo;
        Sort = sort;
        PageNumber = Math.Max(1, pageNumber);
        ArticlesPerPage = await _settings.GetArticlesPerPageAsync();

        AllCategories = await _categories.GetAllAsync();

        var categoryIds = new List<Guid>();
        foreach (var slug in CategorySlugs)
        {
            var found = AllCategories.FirstOrDefault(c =>
                string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                categoryIds.Add(found.Id);
            }
        }

        var articleQuery = new ArticleQuery(
            Page: PageNumber,
            PageSize: ArticlesPerPage,
            TagSlug: string.IsNullOrWhiteSpace(tag) ? null : tag,
            Search: string.IsNullOrWhiteSpace(query) ? null : query,
            Sort: sort,
            DateFrom: dateFrom,
            DateTo: dateTo,
            CategoryIds: categoryIds.Count > 0 ? categoryIds : null,
            HashtagSlugs: HashtagSlugs.Count > 0 ? HashtagSlugs : null,
            Match: Match);

        var (items, total) = await _articles.SearchPublishedAsync(articleQuery);
        Articles = items;
        TotalCount = total;
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)ArticlesPerPage));

        AllHashtags = await _articles.GetPublishedHashtagsAsync();
        AllTags = await _articles.GetAllTagsAsync();
    }

    /// <summary>Combines a comma-separated list with the legacy single-value alias.</summary>
    private static List<string> Merge(string? list, string? single)
    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(list))
        {
            result.AddRange(list.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        if (!string.IsNullOrWhiteSpace(single))
        {
            result.Insert(0, single);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
