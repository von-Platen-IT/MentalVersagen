using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages;

public class IndexModel : PageModel
{
    private const int LatestCount = 6;

    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly ICategoryService _categories;

    public IndexModel(IArticleService articles, IMediaService media, ICategoryService categories)
    {
        _articles = articles;
        _media = media;
        _categories = categories;
    }

    public IReadOnlyList<Article> LatestArticles { get; private set; } = [];

    /// <summary>Regular categories for the landing-page chips (system category excluded).</summary>
    public IReadOnlyList<Category> Topics { get; private set; } = [];

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync()
    {
        var (items, _) = await _articles.SearchPublishedAsync(new ArticleQuery(1, LatestCount));
        LatestArticles = items;

        var categories = await _categories.GetAllAsync();
        Topics = categories.Where(c => !c.IsSystem).ToList();
    }
}
