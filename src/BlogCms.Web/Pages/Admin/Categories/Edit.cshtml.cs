using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Categories;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Categories;

/// <summary>
/// Edits an existing category. The protected system category <c>undefined</c> keeps
/// its name and slug; only description, disclaimer and display order are editable.
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class EditModel : PageModel
{
    private readonly ICategoryService _categories;

    public EditModel(ICategoryService categories)
    {
        _categories = categories;
    }

    public Category Category { get; private set; } = default!;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Slug { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? Disclaimer { get; set; }

    [BindProperty]
    public int DisplayOrder { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var category = await _categories.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        Category = category;
        Name = category.Name;
        Slug = category.Slug;
        Description = category.Description;
        Disclaimer = category.Disclaimer;
        DisplayOrder = category.DisplayOrder;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var category = await _categories.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        Category = category;

        var result = await _categories.UpdateAsync(
            id, new CategoryInput(Name, Slug, Description, Disclaimer, DisplayOrder));

        if (!result.Succeeded)
        {
            if (category.IsSystem)
            {
                // Keep the protected name/slug visible even after a rejected rename.
                Name = category.Name;
                Slug = category.Slug;
            }

            TempData["Error"] = result.Error;
            return Page();
        }

        TempData["Message"] = $"Kategorie „{result.Category!.Name}“ wurde gespeichert.";
        return RedirectToPage("Index");
    }
}