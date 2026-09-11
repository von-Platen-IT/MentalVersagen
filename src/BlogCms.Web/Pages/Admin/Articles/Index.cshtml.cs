using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Content;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly IArticleService _articles;

    public IndexModel(IArticleService articles)
    {
        _articles = articles;
    }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Articles = await _articles.GetForAdminAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _articles.SoftDeleteAsync(id);
        return RedirectToPage();
    }
}
