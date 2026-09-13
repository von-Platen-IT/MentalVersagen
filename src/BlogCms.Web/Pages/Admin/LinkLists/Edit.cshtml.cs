using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.LinkLists;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.LinkLists;

[Authorize(Policy = Policies.RequireAdmin)]
public class EditModel : PageModel
{
    private readonly ILinkListService _linkLists;
    private readonly IArticleService _articles;

    public EditModel(ILinkListService linkLists, IArticleService articles)
    {
        _linkLists = linkLists;
        _articles = articles;
    }

    public LinkList? LinkList { get; private set; }

    public IReadOnlyList<Article> AvailableArticles { get; private set; } = [];

    public Guid LinkListId { get; private set; }

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string? Slug { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var linkList = await _linkLists.GetByIdAsync(id);
        if (linkList is null)
        {
            return NotFound();
        }

        LinkListId = linkList.Id;
        LinkList = linkList;
        Title = linkList.Title;
        Slug = linkList.Slug;
        Description = linkList.Description;

        await LoadAvailableArticlesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        var result = await _linkLists.UpdateAsync(id, Title, Slug, Description);
        TempData[result.Succeeded ? "Message" : "Error"] =
            result.Succeeded ? "Linkliste gespeichert." : result.Error;

        return RedirectToPage("Edit", new { id });
    }

    public Task<IActionResult> OnPostAddItemAsync(Guid id, Guid articleId)
    {
        return MutateAsync(id, ids => ids.Append(articleId), "Beitrag hinzugefügt.");
    }

    public Task<IActionResult> OnPostRemoveItemAsync(Guid id, Guid articleId)
    {
        return MutateAsync(id, ids => ids.Where(x => x != articleId), "Beitrag entfernt.");
    }

    public Task<IActionResult> OnPostMoveAsync(Guid id, Guid articleId, string direction)
    {
        return MutateAsync(id, ids =>
        {
            var list = ids.ToList();
            var index = list.IndexOf(articleId);
            if (index < 0)
            {
                return list;
            }

            var target = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase)
                ? index - 1
                : index + 1;

            if (target < 0 || target >= list.Count)
            {
                return list;
            }

            (list[index], list[target]) = (list[target], list[index]);
            return list;
        }, "Reihenfolge aktualisiert.");
    }

    private async Task<IActionResult> MutateAsync(
        Guid id, Func<IEnumerable<Guid>, IEnumerable<Guid>> mutate, string message)
    {
        var linkList = await _linkLists.GetByIdAsync(id);
        if (linkList is null)
        {
            return NotFound();
        }

        var current = linkList.Items
            .OrderBy(i => i.Position)
            .Select(i => i.ArticleId)
            .ToList();

        await _linkLists.SetItemsAsync(id, mutate(current));
        TempData["Message"] = message;
        return RedirectToPage("Edit", new { id });
    }

    private async Task LoadAvailableArticlesAsync()
    {
        var (items, _) = await _articles.SearchPublishedAsync(new ArticleQuery(1, 50));
        AvailableArticles = items;
    }
}
