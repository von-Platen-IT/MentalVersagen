using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Membership;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly IMembershipSyncService _membershipSync;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        IMembershipSyncService membershipSync,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _membershipSync = membershipSync;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
        [EmailAddress]
        [Display(Name = "E-Mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte ein Passwort angeben.")]
        [DataType(DataType.Password)]
        [Display(Name = "Passwort")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Angemeldet bleiben")]
        public bool RememberMe { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // RequireConfirmedEmail is enforced via SignIn options; unconfirmed
        // accounts get IsNotAllowed and are told to confirm first.
        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Refresh a stale membership profile on login. Failures must never
            // block the login; the last known status stays in effect.
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user is not null)
            {
                try
                {
                    await _membershipSync.SyncUserIfStaleAsync(user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex, "Membership login sync failed for user {UserId}.", user.Id);
                }
            }

            _logger.LogInformation("User {Email} logged in.", Input.Email);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} is locked out.", Input.Email);
            ModelState.AddModelError(string.Empty, "Das Konto ist vorübergehend gesperrt. Bitte später erneut versuchen.");
            return Page();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Bitte bestätige zuerst deine E-Mail-Adresse.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "E-Mail oder Passwort ist falsch.");
        return Page();
    }
}
