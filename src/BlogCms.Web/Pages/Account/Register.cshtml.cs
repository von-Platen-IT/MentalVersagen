using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Captcha;
using BlogCms.Infrastructure.Email;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly IAppEmailSender _emailSender;
    private readonly ICaptchaService _captcha;
    private readonly IRegistrationThrottle _throttle;

    public RegisterModel(
        UserManager<User> userManager,
        IAppEmailSender emailSender,
        ICaptchaService captcha,
        IRegistrationThrottle throttle)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _captcha = captcha;
        _throttle = throttle;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Answer to the arithmetic captcha question.</summary>
    [BindProperty]
    [Display(Name = "Sicherheitsabfrage")]
    public string? CaptchaAnswer { get; set; }

    /// <summary>
    /// Signed token carrying the expected answer. It is only ever validated on
    /// the server — the client cannot read or forge the expected value.
    /// </summary>
    [BindProperty]
    public string? CaptchaToken { get; set; }

    /// <summary>Honeypot field: hidden from humans, must stay empty.</summary>
    [BindProperty]
    public string? ExtraField { get; set; }

    public string CaptchaQuestion { get; private set; } = string.Empty;

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
        IssueCaptcha();
    }

    /// <summary>
    /// Hands out a fresh challenge for the "neue Aufgabe" button (AJAX).
    /// </summary>
    public IActionResult OnGetCaptcha()
    {
        var challenge = _captcha.Issue();
        return new JsonResult(new { question = challenge.Question, token = challenge.Token });
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        // Drosselung: begrenzt die Registrierungsversuche pro IP-Adresse.
        if (!_throttle.TryAcquire(HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError(
                string.Empty,
                "Zu viele Registrierungsversuche. Bitte in einer Minute erneut versuchen.");
            IssueCaptcha();
            return Page();
        }

        // Honeypot: bots fill every field they find — reject silently.
        if (!string.IsNullOrWhiteSpace(ExtraField))
        {
            ModelState.AddModelError(string.Empty, "Die Registrierung konnte nicht verarbeitet werden.");
            IssueCaptcha();
            return Page();
        }

        ValidateCaptcha();

        if (!ModelState.IsValid)
        {
            IssueCaptcha();
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
            IssueCaptcha();
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

    private void IssueCaptcha()
    {
        var challenge = _captcha.Issue();
        CaptchaQuestion = challenge.Question;
        CaptchaToken = challenge.Token;
    }

    private void ValidateCaptcha()
    {
        var result = _captcha.Validate(CaptchaToken, CaptchaAnswer);
        if (result == CaptchaResult.Valid)
        {
            return;
        }

        var message = result switch
        {
            CaptchaResult.TooFast => "Bitte nimm dir einen Moment Zeit für die Sicherheitsabfrage.",
            CaptchaResult.Expired => "Die Sicherheitsabfrage ist abgelaufen. Bitte löse die neue Aufgabe.",
            CaptchaResult.InvalidToken => "Die Sicherheitsabfrage ist ungültig. Bitte löse die neue Aufgabe.",
            _ => "Die Antwort auf die Sicherheitsabfrage ist nicht korrekt."
        };

        ModelState.AddModelError(nameof(CaptchaAnswer), message);
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
