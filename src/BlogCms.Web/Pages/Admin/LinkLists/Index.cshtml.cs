using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.LinkLists;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.LinkLists;

[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly ILinkListService _linkLists;

    public IndexModel(ILinkListService linkLists)
    {
        _linkLists = linkLists;
    }

    public IReadOnlyList<LinkList> LinkLists { get; private set; } = [];

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string? Slug { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public async Task OnGetAsync()
    {
        LinkLists = await _linkLists.GetAllAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var result = await _linkLists.CreateAsync(Title, Slug, Description);
        TempData[result.Succeeded ? "Message" : "Error"] =
            result.Succeeded ? $"Linkliste „{Title}“ wurde angelegt." : result.Error;

        if (result.Succeeded && result.LinkList is not null)
        {
            return RedirectToPage("Edit", new { id = result.LinkList.Id });
        }

        LinkLists = await _linkLists.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _linkLists.DeleteAsync(id);
        TempData["Message"] = "Linkliste gelöscht.";
        return RedirectToPage();
    }
}
