using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly IAppEmailSender _emailSender;

    public RegisterModel(UserManager<User> userManager, IAppEmailSender emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte einen Anzeigenamen angeben.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Der Anzeigename muss 2 bis 100 Zeichen lang sein.")]
        [Display(Name = "Anzeigename")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse.")]
        [Display(Name = "E-Mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte ein Passwort angeben.")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "Das Passwort muss mindestens 10 Zeichen lang sein.")]
        [DataType(DataType.Password)]
        [Display(Name = "Passwort")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Passwort bestätigen")]
        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string ConfirmPassword { get; set; } = string.Empty;
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

        var user = new User
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Role = UserRole.Reader,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }

        // Every new account starts as a Reader role member.
        await _userManager.AddToRoleAsync(user, UserRole.Reader.ToString());

        // Send the double-opt-in style confirmation link. The account cannot be
        // used for commenting/subscribing until EmailConfirmed is true.
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { userId = user.Id, code },
            protocol: Request.Scheme)!;

        var html =
            $"""
            <p>Hallo {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
            <p>willkommen bei MentalVersagen. Bitte bestätige deine E-Mail-Adresse:</p>
            <p><a href="{callbackUrl}">E-Mail-Adresse bestätigen</a></p>
            """;

        await _emailSender.SendAsync(user.Email!, "E-Mail-Adresse bestätigen", html);

        return RedirectToPage("/Account/RegisterConfirmation", new { email = Input.Email });
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
