using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
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
    private readonly UserManager<User> _userManager;

    public IndexModel(IArticleService articles, UserManager<User> userManager)
    {
        _articles = articles;
        _userManager = userManager;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    public async Task OnGetAsync()
    {
        // Admins manage all articles; authors only see their own (FeatureFix1 BR-013/BR-014).
        if (IsAdmin)
        {
            Articles = await _articles.GetForAdminAsync();
            return;
        }

        var userId = Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;
        Articles = await _articles.GetForAuthorAsync(userId);
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
