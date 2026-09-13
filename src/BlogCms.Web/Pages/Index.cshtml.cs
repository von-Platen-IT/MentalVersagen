using BlogCms.Domain.Entities;
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

    public IndexModel(IArticleService articles, IMediaService media)
    {
        _articles = articles;
        _media = media;
    }

    public IReadOnlyList<Article> LatestArticles { get; private set; } = [];

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync()
    {
        var (items, _) = await _articles.SearchPublishedAsync(new ArticleQuery(1, LatestCount));
        LatestArticles = items;
    }
}
