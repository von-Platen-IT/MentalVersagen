using BlogCms.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public ConfirmEmailModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public string StatusMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return RedirectToPage("/Index");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            StatusMessage = "Benutzer nicht gefunden.";
            return Page();
        }

        var result = await _userManager.ConfirmEmailAsync(user, code);
        StatusMessage = result.Succeeded
            ? "Deine E-Mail-Adresse wurde bestätigt. Du kannst dich jetzt anmelden."
            : "Die Bestätigung ist fehlgeschlagen oder der Link ist abgelaufen.";

        return Page();
    }
}
