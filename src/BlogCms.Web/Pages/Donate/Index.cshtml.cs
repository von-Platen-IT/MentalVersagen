using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Donate;

public class IndexModel : PageModel
{
    private readonly IStripeService _stripeService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly UserManager<User> _userManager;

    public IndexModel(
        IStripeService stripeService,
        ISubscriptionService subscriptionService,
        UserManager<User> userManager)
    {
        _stripeService = stripeService;
        _subscriptionService = subscriptionService;
        _userManager = userManager;
    }

    [BindProperty]
    public decimal Amount { get; set; } = 10m;

    [BindProperty]
    public string Currency { get; set; } = "EUR";

    [BindProperty]
    public string? Email { get; set; }

    public string? Message { get; private set; }

    public void OnGet(int thanks = 0)
    {
        if (thanks == 1)
        {
            Message = "Vielen Dank für deine Spende!";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Amount <= 0)
        {
            Message = "Bitte einen gültigen Betrag angeben.";
            return Page();
        }

        var userId = _userManager.GetUserId(User) is { } id && Guid.TryParse(id, out var parsed)
            ? parsed
            : (Guid?)null;

        // Without configured Stripe keys, record the donation directly (dev).
        if (!_stripeService.IsConfigured)
        {
            await _subscriptionService.RecordDonationAsync(
                Amount, Currency, PaymentProvider.Stripe,
                $"don_fake_{Guid.NewGuid():N}", userId, DonationStatus.Completed, cancellationToken);

            Message = "Vielen Dank für deine Spende! (Entwicklungsmodus, keine echte Zahlung)";
            return Page();
        }

        var successUrl = Url.Page("/Donate/Index", null, new { thanks = 1 }, Request.Scheme)!;
        var cancelUrl = Url.Page("/Donate/Index", null, null, Request.Scheme)!;

        var session = await _stripeService.CreateDonationCheckoutAsync(
            Amount, Currency, userId, Email, successUrl, cancelUrl, cancellationToken);

        return Redirect(session.Url);
    }
}
