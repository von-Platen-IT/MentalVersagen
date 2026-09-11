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

    public SubscribeModel(
        INewsletterService newsletterService,
        IAppEmailSender emailSender,
        UserManager<User> userManager)
    {
        _newsletterService = newsletterService;
        _emailSender = emailSender;
        _userManager = userManager;
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

        await _emailSender.SendAsync(Email, "Newsletter-Anmeldung bestätigen", html, cancellationToken);

        Message = "Fast fertig: Bitte bestätige den Link in der soeben gesendeten E-Mail.";
        ModelState.Clear();
        return Page();
    }
}
