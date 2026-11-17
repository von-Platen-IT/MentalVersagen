using BlogCms.Infrastructure.Settings;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Settings;

/// <summary>
/// Admin page for runtime-editable display settings. Currently exposes the number
/// of articles shown per list (landing page, search and REST grid).
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly ISettingsService _settings;

    public IndexModel(ISettingsService settings)
    {
        _settings = settings;
    }

    [BindProperty]
    public int ArticlesPerPage { get; set; }

    public async Task OnGetAsync()
    {
        ArticlesPerPage = await _settings.GetArticlesPerPageAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _settings.SetArticlesPerPageAsync(ArticlesPerPage);
        TempData["Message"] = $"Beiträge pro Liste: {ArticlesPerPage}.";
        return RedirectToPage();
    }
}
