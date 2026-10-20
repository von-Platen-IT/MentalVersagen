using System.ComponentModel.DataAnnotations;
using BlogCms.Infrastructure.Captcha;
using BlogCms.Infrastructure.Contact;
using BlogCms.Infrastructure.Email;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Pages;

/// <summary>
/// Kontaktformular. Die Nachricht wird per E-Mail an die konfigurierte
/// Zieladresse gesendet; der Besucher bekommt eine neutrale Bestätigung.
/// Spam wird durch Captcha, Honeypot und eine IP-Drosselung abgewehrt.
/// </summary>
public class KontaktModel : PageModel
{
    private const string FallbackToAddress = "bernd@mentalversagen.de";

    private readonly IAppEmailSender _emailSender;
    private readonly ICaptchaService _captcha;
    private readonly IContactThrottle _throttle;
    private readonly ContactOptions _options;
    private readonly ILogger<KontaktModel> _logger;

    public KontaktModel(
        IAppEmailSender emailSender,
        ICaptchaService captcha,
        IContactThrottle throttle,
        IOptions<ContactOptions> options,
        ILogger<KontaktModel> logger)
    {
        _emailSender = emailSender;
        _captcha = captcha;
        _throttle = throttle;
        _options = options.Value;
        _logger = logger;
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

    public string? Message { get; private set; }

    public bool MessageIsError { get; private set; }

    /// <summary>Adresse, die auf der Seite genannt wird.</summary>
    public string ToAddress =>
        string.IsNullOrWhiteSpace(_options.ToAddress) ? FallbackToAddress : _options.ToAddress;

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Der Name muss 2 bis 100 Zeichen lang sein.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse.")]
        [StringLength(200, ErrorMessage = "Die E-Mail-Adresse ist zu lang.")]
        [Display(Name = "E-Mail")]
        public string Email { get; set; } = string.Empty;

        [StringLength(150, ErrorMessage = "Der Betreff darf höchstens 150 Zeichen lang sein.")]
        [Display(Name = "Betreff (optional)")]
        public string? Subject { get; set; }

        [Required(ErrorMessage = "Bitte eine Nachricht angeben.")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Die Nachricht muss 10 bis 5000 Zeichen lang sein.")]
        [Display(Name = "Nachricht")]
        public string Message { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true",
            ErrorMessage = "Bitte der Verarbeitung der Angaben zustimmen.")]
        [Display(Name = "Ich habe die Datenschutzerklärung gelesen und stimme der Verarbeitung meiner Angaben zu.")]
        public bool PrivacyAccepted { get; set; }
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

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Drosselung: begrenzt die Sendeversuche pro IP-Adresse.
        if (!_throttle.TryAcquire(HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            MessageIsError = true;
            Message = "Zu viele Nachrichten in kurzer Zeit. Bitte später erneut versuchen.";
            IssueCaptcha();
            return Page();
        }

        // Honeypot: bots fill every field they find. Pretend success so the bot
        // gets no signal, but never send a mail.
        if (!string.IsNullOrWhiteSpace(ExtraField))
        {
            _logger.LogWarning("Kontaktformular: Honeypot ausgelöst, Nachricht verworfen.");
            Message = "Vielen Dank für deine Nachricht.";
            Input = new InputModel();
            IssueCaptcha();
            ModelState.Clear();
            return Page();
        }

        ValidateCaptcha();

        if (!ModelState.IsValid)
        {
            IssueCaptcha();
            return Page();
        }

        var subject = ContactMessageBuilder.BuildSubject(Input.Subject);
        var html = ContactMessageBuilder.BuildBody(
            Input.Name, Input.Email, Input.Subject, Input.Message);

        try
        {
            await _emailSender.SendAsync(
                ToAddress,
                subject,
                html,
                cancellationToken,
                replyTo: Input.Email.Trim());

            Message = "Vielen Dank für deine Nachricht. Ich melde mich so bald wie möglich.";
            Input = new InputModel();
            IssueCaptcha();
            ModelState.Clear();
        }
        catch (Exception ex)
        {
            // Keine internen Details an den Besucher weitergeben.
            _logger.LogError(ex, "Kontaktnachricht konnte nicht versendet werden.");
            MessageIsError = true;
            Message = "Die Nachricht konnte gerade nicht versendet werden. Bitte später erneut versuchen.";
            IssueCaptcha();
        }

        return Page();
    }

    private void IssueCaptcha()
    {
        var challenge = _captcha.Issue();
        CaptchaQuestion = challenge.Question;
        CaptchaToken = challenge.Token;

        // Die Aufgabe ist neu — die bisherige Antwort passt nicht mehr dazu.
        CaptchaAnswer = null;

        // Alte POST-Werte aus dem ModelState entfernen, damit die Tag Helper
        // nicht den alten Token zusammen mit der neuen Frage rendern.
        ModelState.Remove(nameof(CaptchaToken));
        ModelState.Remove(nameof(CaptchaAnswer));
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
}
