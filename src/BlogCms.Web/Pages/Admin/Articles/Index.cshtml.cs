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

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    private Guid CurrentUserId =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;

    public string? TitleImageUrl(Article article) => BlogCms.Web.Content.ArticleDisplay.ResolveTitleImage(article, _media);

    public async Task OnGetAsync()
    {
        ArticleStatus? statusFilter = Enum.TryParse<ArticleStatus>(Filter, ignoreCase: true, out var parsed)
            ? parsed
            : null;

        Guid? authorFilter = IsAdmin ? null : CurrentUserId;
        Articles = await _articles.GetForManagementAsync(authorFilter, statusFilter, Search);
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(Guid id, ArticleStatus targetStatus)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return RedirectToPage(new { Filter, Search });
        }

        if (!IsAdmin && article.AuthorId != CurrentUserId)
        {
            return Forbid();
        }

        if (targetStatus == ArticleStatus.Published)
        {
            var hasCover = !string.IsNullOrWhiteSpace(article.TitleImageUrl) || article.MediaAssets.Count > 0;
            if (!hasCover || string.IsNullOrWhiteSpace(article.Excerpt) || string.IsNullOrWhiteSpace(article.ContentMarkdown))
            {
                TempData["Error"] = "Zum Veröffentlichen müssen Titelbild, Kurzbeschreibung und Inhalt vorhanden sein.";
                return RedirectToPage(new { Filter, Search });
            }
        }

        article.Status = targetStatus;
        await _articles.UpdateAsync(
            article,
            article.ArticleTags.Select(at => at.Tag!.Name),
            article.ArticleHashtags.Select(ah => ah.Hashtag!.Name));

        TempData["Message"] = $"Status für „{article.Title}“ auf {BlogCms.Web.Content.ArticleDisplay.StatusLabel(targetStatus)} geändert.";
        return RedirectToPage(new { Filter, Search });
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
