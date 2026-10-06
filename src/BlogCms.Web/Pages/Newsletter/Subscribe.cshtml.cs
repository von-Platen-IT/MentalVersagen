using System.ComponentModel.DataAnnotations;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Email;
using BlogCms.Infrastructure.Newsletter;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Newsletter;

public class SubscribeModel : PageModel
{
    private readonly INewsletterService _newsletterService;
    private readonly IAppEmailSender _emailSender;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<SubscribeModel> _logger;

    public SubscribeModel(
        INewsletterService newsletterService,
        IAppEmailSender emailSender,
        UserManager<User> userManager,
        ILogger<SubscribeModel> logger)
    {
        _newsletterService = newsletterService;
        _emailSender = emailSender;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
    [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse.")]
    [Display(Name = "E-Mail")]
    public string Email { get; set; } = string.Empty;

    public string? Message { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Guid? userId = _userManager.GetUserId(User) is { } id && Guid.TryParse(id, out var parsed)
            ? parsed
            : null;

        var result = await _newsletterService.SubscribeAsync(Email, userId, cancellationToken);

        if (result.AlreadyConfirmed)
        {
            Message = "Diese E-Mail-Adresse ist bereits für den Newsletter angemeldet.";
            return Page();
        }

        var confirmUrl = Url.Page("/Newsletter/Confirm", null, new { token = result.Token }, Request.Scheme)!;
        var unsubscribeUrl = Url.Page("/Newsletter/Unsubscribe", null, new { token = result.Token }, Request.Scheme)!;

        var html =
            $"""
            <p>Bitte bestätige deine Newsletter-Anmeldung (Double-Opt-in):</p>
            <p><a href="{confirmUrl}">Anmeldung bestätigen</a></p>
            <hr />
            <p style="font-size:12px">Kein Interesse? <a href="{unsubscribeUrl}">Abmelden</a></p>
            """;

        // Das Abo besteht zu diesem Zeitpunkt bereits. Ein Versandfehler darf es
        // daher nicht verwerfen — der Nutzer bekommt stattdessen eine klare Meldung.
        try
        {
            await _emailSender.SendAsync(Email, "Newsletter-Anmeldung bestätigen", html, cancellationToken);
            Message = "Fast fertig: Bitte bestätige den Link in der soeben gesendeten E-Mail.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bestätigungs-E-Mail für das Newsletter-Abo konnte nicht gesendet werden.");
            Message = "Die Bestätigungs-E-Mail konnte gerade nicht versendet werden. Bitte später erneut versuchen.";
        }

        ModelState.Clear();
        return Page();
    }
}
