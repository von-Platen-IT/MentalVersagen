using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

[Authorize(Policy = Policies.RequireAuthor)]
public class IndexModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly UserManager<User> _userManager;

    public IndexModel(IArticleService articles, IMediaService media, UserManager<User> userManager)
    {
        _articles = articles;
        _media = media;
        _userManager = userManager;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    public string? TitleImageUrl(Article article) => BlogCms.Web.Content.ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync()
    {
        IReadOnlyList<Article> all;
        if (IsAdmin)
        {
            all = await _articles.GetForAdminAsync();
        }
        else
        {
            var userId = Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;
            all = await _articles.GetForAuthorAsync(userId);
        }

        if (string.Equals(Filter, "Published", StringComparison.OrdinalIgnoreCase))
        {
            Articles = all.Where(a => a.Status == ArticleStatus.Published).ToList();
        }
        else if (string.Equals(Filter, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            Articles = all.Where(a => a.Status == ArticleStatus.Draft).ToList();
        }
        else if (string.Equals(Filter, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            Articles = all.Where(a => a.Status == ArticleStatus.Scheduled).ToList();
        }
        else
        {
            Articles = all;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return RedirectToPage();
        }

        // Ownership check server-side: authors may only delete their own articles.
        if (!IsAdmin && article.AuthorId != (Guid.TryParse(_userManager.GetUserId(User), out var uid) ? uid : Guid.Empty))
        {
            return Forbid();
        }

        await _articles.SoftDeleteAsync(id);
        return RedirectToPage();
    }
}
