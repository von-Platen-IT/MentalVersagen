using BlogCms.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<IndexModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public User? CurrentUser { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUser = await _userManager.GetUserAsync(User);
        if (CurrentUser is null)
        {
            return RedirectToPage("/Account/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Soft-delete + anonymize personal data, while keeping authored content
        // structurally intact (articles/comments then show "Gelöschter Nutzer").
        var anonymizedEmail = $"deleted-{user.Id:N}@deleted.invalid";
        user.Email = anonymizedEmail;
        user.UserName = anonymizedEmail;
        user.NormalizedEmail = anonymizedEmail.ToUpperInvariant();
        user.NormalizedUserName = anonymizedEmail.ToUpperInvariant();
        user.DisplayName = "Gelöschter Nutzer";
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.DeletedAt = DateTime.UtcNow;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("User {UserId} deleted their account (soft-delete + anonymization).", user.Id);

        await _signInManager.SignOutAsync();

        return RedirectToPage("/Account/AccountDeleted");
    }
}
