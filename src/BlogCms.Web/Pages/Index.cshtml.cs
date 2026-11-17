using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Settings;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;

    public IndexModel(
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

    public IReadOnlyList<Article> LatestArticles { get; private set; } = [];

    /// <summary>Regular categories for the landing-page chips (system category excluded).</summary>
    public IReadOnlyList<Category> Topics { get; private set; } = [];

    /// <summary>Categories currently filtered via the URL (bookmarkable).</summary>
    public IReadOnlyList<Category> ActiveCategories { get; private set; } = [];

    /// <summary>Hashtags currently filtered via the URL (bookmarkable).</summary>
    public IReadOnlyList<HashtagUsage> ActiveHashtags { get; private set; } = [];

    public ArticleFilterMatch Match { get; private set; } = ArticleFilterMatch.Any;

    public ArticleSortOrder Sort { get; private set; } = ArticleSortOrder.Newest;

    /// <summary>Admin-configured number of articles shown per list.</summary>
    public int ArticlesPerPage { get; private set; } = SettingKeys.DefaultArticlesPerPage;

    public bool HasActiveFilter => ActiveCategories.Count > 0 || ActiveHashtags.Count > 0;

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync(
        string? categories = null,
        string? hashtags = null,
        string? match = null,
        string? sort = null)
    {
        Match = Enum.TryParse<ArticleFilterMatch>(match, ignoreCase: true, out var parsedMatch)
            ? parsedMatch
            : ArticleFilterMatch.Any;
        Sort = Enum.TryParse<ArticleSortOrder>(sort, ignoreCase: true, out var parsedSort)
            ? parsedSort
            : ArticleSortOrder.Newest;

        ArticlesPerPage = await _settings.GetArticlesPerPageAsync();

        var categorySlugs = ParseList(categories);
        var hashtagSlugs = ParseList(hashtags);

        var allCategories = await _categories.GetAllAsync();
        Topics = allCategories.Where(c => !c.IsSystem).ToList();

        var categoryIds = new List<Guid>();
        foreach (var slug in categorySlugs)
        {
            var found = allCategories.FirstOrDefault(c =>
                string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                categoryIds.Add(found.Id);
            }
        }

        ActiveCategories = categoryIds
            .Select(id => allCategories.FirstOrDefault(c => c.Id == id))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        var (items, _) = await _articles.SearchPublishedAsync(new ArticleQuery(
            Page: 1,
            PageSize: ArticlesPerPage,
            CategoryIds: categoryIds.Count > 0 ? categoryIds : null,
            HashtagSlugs: hashtagSlugs.Count > 0 ? hashtagSlugs : null,
            Match: Match,
            Sort: Sort));
        LatestArticles = items;

        if (hashtagSlugs.Count > 0)
        {
            var publishedHashtags = await _articles.GetPublishedHashtagsAsync();
            ActiveHashtags = hashtagSlugs
                .Select(slug => publishedHashtags.FirstOrDefault(h =>
                    string.Equals(h.Slug, slug, StringComparison.OrdinalIgnoreCase))
                    ?? new HashtagUsage(slug, slug, 0))
                .ToList();
        }
    }

    /// <summary>
    /// Builds a bookmarkable URL for the current filter selection, optionally
    /// removing one category or hashtag (used by the filter chips).
    /// </summary>
    public string BuildFilterUrl(string? removeCategory = null, string? removeHashtag = null)
    {
        var categories = ActiveCategories
            .Select(c => c.Slug)
            .Where(s => !string.Equals(s, removeCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hashtags = ActiveHashtags
            .Select(h => h.Slug)
            .Where(s => !string.Equals(s, removeHashtag, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var query = new List<string>();
        if (categories.Count > 0)
        {
            query.Add("categories=" + Uri.EscapeDataString(string.Join(',', categories)));
        }
        if (hashtags.Count > 0)
        {
            query.Add("hashtags=" + Uri.EscapeDataString(string.Join(',', hashtags)));
        }
        if (Match == ArticleFilterMatch.All)
        {
            query.Add("match=all");
        }
        if (Sort != ArticleSortOrder.Newest)
        {
            query.Add("sort=" + Sort);
        }

        return query.Count == 0 ? "/" : "/?" + string.Join('&', query);
    }

    private static List<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
