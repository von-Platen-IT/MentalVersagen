using BlogCms.Infrastructure.Categories;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Categories;

/// <summary>
/// Admin catalogue of editorial categories. The overview lists all categories with
/// their article counts and allows creating and deleting categories. The protected
/// system category <c>undefined</c> is rendered without destructive actions.
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly ICategoryService _categories;

    public IndexModel(ICategoryService categories)
    {
        _categories = categories;
    }

    public IReadOnlyList<CategoryUsage> Categories { get; private set; } = [];

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Slug { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? Disclaimer { get; set; }

    [BindProperty]
    public string Color { get; set; } = "#7f8c8d";

    [BindProperty]
    public int DisplayOrder { get; set; }

    public async Task OnGetAsync()
    {
        Categories = await _categories.GetUsageAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var result = await _categories.CreateAsync(
            new CategoryInput(Name, Slug, Description, Disclaimer, DisplayOrder, Color));

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            Categories = await _categories.GetUsageAsync();
            return Page();
        }

        TempData["Message"] = $"Kategorie „{result.Category!.Name}“ wurde angelegt.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var result = await _categories.DeleteAsync(id);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "Kategorie gelöscht. Zugehörige Artikel wurden zu „undefined“ verschoben."
            : result.Error;

        return RedirectToPage();
    }
}