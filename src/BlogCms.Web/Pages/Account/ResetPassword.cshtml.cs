using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(UserManager<User> userManager, ILogger<ResetPasswordModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>User id from the reset link.</summary>
    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    /// <summary>Identity reset token from the reset link.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Code { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte ein neues Passwort angeben.")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Das Passwort muss mindestens 10 Zeichen lang sein.")]
        [DataType(DataType.Password)]
        [Display(Name = "Neues Passwort")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Passwort bestätigen")]
        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        UserId = userId;
        Code = code;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(
                string.Empty,
                "Der Link ist unvollständig. Bitte fordere einen neuen Link an.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null || user.DeletedAt is not null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Der Link ist ungültig oder abgelaufen. Bitte fordere einen neuen Link an.");
            return Page();
        }

        var result = await _userManager.ResetPasswordAsync(user, Code, Input.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} reset their password.", user.Id);
            return RedirectToPage("/Account/ResetPasswordConfirmation");
        }

        // Ein abgelaufener oder bereits verwendeter Token ist der häufigste
        // Fehlerfall. Die konkrete Identity-Fehlermeldung ist für Leser wenig
        // hilfreich, daher wird sie durch einen klaren Hinweis ersetzt.
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            ModelState.AddModelError(
                string.Empty,
                "Der Link ist ungültig oder abgelaufen. Bitte fordere einen neuen Link an.");
            return Page();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
