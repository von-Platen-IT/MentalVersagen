using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Articles;

public class IndexModel : PageModel
{
    private const int PageSize = 10;

    private readonly IArticleService _articles;

    public IndexModel(IArticleService articles)
    {
        _articles = articles;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public IReadOnlyList<Tag> AllTags { get; private set; } = [];

    public int PageNumber { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public ArticleCategory? Category { get; private set; }

    public string? TagSlug { get; private set; }

    public async Task OnGetAsync(int pageNumber = 1, ArticleCategory? category = null, string? tag = null)
    {
        PageNumber = Math.Max(1, pageNumber);
        Category = category;
        TagSlug = tag;

        var (items, total) = await _articles.GetPublishedAsync(PageNumber, PageSize, category, tag);
        Articles = items;
        AllTags = await _articles.GetAllTagsAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
    }
}
