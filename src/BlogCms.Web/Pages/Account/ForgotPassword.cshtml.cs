using System.ComponentModel.DataAnnotations;
using BlogCms.Web.Extensions;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Email;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly IAppEmailSender _emailSender;
    private readonly IPasswordResetThrottle _throttle;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<User> userManager,
        IAppEmailSender emailSender,
        IPasswordResetThrottle throttle,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _throttle = throttle;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse.")]
        [Display(Name = "E-Mail")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Drosselung: begrenzt die Anfragen pro IP-Adresse, damit das Formular
        // weder zum Mail-Flooding noch zum schnellen Durchprobieren von Adressen
        // missbraucht werden kann.
        if (!_throttle.TryAcquire(HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError(
                string.Empty,
                "Zu viele Anfragen. Bitte in einer Minute erneut versuchen.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Anti-Enumeration: Unabhängig davon, ob die Adresse existiert, wird
        // immer dieselbe neutrale Bestätigungsseite angezeigt. Ein Angreifer
        // kann daraus nicht ableiten, ob ein Konto vorhanden ist.
        if (user is not null && user.DeletedAt is null)
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            // Absolute URL für die E-Mail: öffentliche Basis-URL (Site__PublicBaseUrl
            // oder Schema/Host inkl. X-Forwarded-Proto/Host) + relativer Seitenpfad.
            var callbackUrl = HttpContext.GetPublicBaseUrl() + Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { userId = user.Id, code });

            var html =
                $"""
                <p>Hallo {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
                <p>für dein Konto bei MentalVersagen wurde ein neues Passwort angefordert.
                Klicke auf den folgenden Link, um ein neues Passwort zu vergeben:</p>
                <p><a href="{callbackUrl}">Neues Passwort vergeben</a></p>
                <p>Der Link ist 24 Stunden gültig und kann nur einmal verwendet werden.</p>
                <p>Wenn du kein neues Passwort angefordert hast, kannst du diese E-Mail
                ignorieren. Dein Passwort bleibt unverändert.</p>
                """;

            // Ein Versandfehler darf die Anfrage nicht abbrechen: der Nutzer
            // sieht sonst eine Fehlerseite und erfährt indirekt, dass das Konto
            // existiert. Der Fehler wird nur serverseitig protokolliert.
            try
            {
                await _emailSender.SendAsync(user.Email!, "Passwort zurücksetzen", html);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Passwort-Reset-E-Mail an {Email} konnte nicht gesendet werden.",
                    user.Email);
            }
        }

        return RedirectToPage("/Account/ForgotPasswordConfirmation");
    }
}
