using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Articles;

public class IndexModel : PageModel
{
    private const int PageSize = 10;

    private readonly IArticleService _articles;
    private readonly IMediaService _media;

    public IndexModel(IArticleService articles, IMediaService media)
    {
        _articles = articles;
        _media = media;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public IReadOnlyList<Tag> AllTags { get; private set; } = [];

    public IReadOnlyList<Hashtag> AllHashtags { get; private set; } = [];

    public IReadOnlyList<User> AllAuthors { get; private set; } = [];

    public int PageNumber { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public ArticleCategory? Category { get; private set; }

    public string? TagSlug { get; private set; }

    public string? HashtagSlug { get; private set; }

    public Guid? AuthorId { get; private set; }

    public string? Query { get; private set; }

    public ArticleAccessLevel? AccessLevel { get; private set; }

    public ArticleSortOrder Sort { get; private set; } = ArticleSortOrder.Newest;

    public string? TitleImageUrl(Article article) => ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync(
        int pageNumber = 1,
        ArticleCategory? category = null,
        string? tag = null,
        string? hashtag = null,
        Guid? authorId = null,
        string? query = null,
        ArticleAccessLevel? access = null,
        ArticleSortOrder sort = ArticleSortOrder.Newest)
    {
        PageNumber = Math.Max(1, pageNumber);
        Category = category;
        TagSlug = tag;
        HashtagSlug = hashtag;
        AuthorId = authorId;
        Query = query;
        AccessLevel = access;
        Sort = sort;

        var queryModel = new ArticleQuery(
            PageNumber, PageSize, category, tag, hashtag, authorId, query, access, sort);

        var (items, total) = await _articles.SearchPublishedAsync(queryModel);
        Articles = items;
        AllTags = await _articles.GetAllTagsAsync();
        AllHashtags = await _articles.GetAllHashtagsAsync();
        AllAuthors = await _articles.GetPublishedAuthorsAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
    }
}
