using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.LinkLists;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.LinkLists;

/// <summary>
/// Minimal, layout-free rendering of a link list so it can be embedded on other
/// pages/sites via an iframe (FeatureFix1 BR-092).
/// </summary>
public class EmbedModel : PageModel
{
    private readonly ILinkListService _linkLists;

    public EmbedModel(ILinkListService linkLists)
    {
        _linkLists = linkLists;
    }

    public LinkList? LinkList { get; private set; }

    public IReadOnlyList<Article> Articles { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var linkList = await _linkLists.GetBySlugAsync(slug);
        if (linkList is null)
        {
            return NotFound();
        }

        LinkList = linkList;
        Articles = linkList.Items
            .OrderBy(i => i.Position)
            .Select(i => i.Article)
            .Where(a => a is not null && a.Status == ArticleStatus.Published && a.PublishedAt != null)
            .Select(a => a!)
            .ToList();

        return Page();
    }
}
