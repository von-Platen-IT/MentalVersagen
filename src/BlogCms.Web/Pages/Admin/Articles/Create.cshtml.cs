using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Web.Authorization;
using BlogCms.Web.Content;
using BlogCms.Web.Extensions;
using BlogCms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

/// <summary>
/// Presentation only: binds the form, delegates the whole posting workflow to
/// IArticlePostingService and maps field errors onto ModelState.
/// </summary>
[Authorize(Policy = Policies.RequireAuthor)]
public class CreateModel : PageModel
{
    private readonly IArticlePostingService _posting;
    private readonly UserManager<User> _userManager;

    public CreateModel(IArticlePostingService posting, UserManager<User> userManager)
    {
        _posting = posting;
        _userManager = userManager;
    }

    [BindProperty]
    public ArticleInputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public Task<IActionResult> OnPostAsync()
    {
        return PostAsync(Input.Status);
    }

    public Task<IActionResult> OnPostPublishAsync()
    {
        // Geschäftsregel „geplant mit Zukunftsdatum vs. sofort veröffentlichen“
        // liegt im ArticleValidator, nicht mehr im PageModel.
        return PostAsync(ArticleValidator.ResolveTargetStatus(Input.Status, Input.ScheduledAt));
    }

    public Task<IActionResult> OnPostSaveDraftAsync()
    {
        return PostAsync(ArticleStatus.Draft);
    }

    private async Task<IActionResult> PostAsync(ArticleStatus requestedStatus)
    {
        var authorIdValue = _userManager.GetUserId(User);
        if (authorIdValue is null || !Guid.TryParse(authorIdValue, out var authorId))
        {
            return Challenge();
        }

        var result = await _posting.CreateAsync(
            Input.ToRequest(requestedStatus), authorId, HttpContext.GetClientIp());

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return Page();
        }

        TempData["Message"] = ArticleMessages.ForCreate(result.Article!);
        return RedirectToPage("Index");
    }
}
