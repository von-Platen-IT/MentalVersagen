using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Payments;
using BlogCms.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Pages.Membership;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IStripeService _stripeService;
    private readonly PaymentOptions _options;
    private readonly UserManager<User> _userManager;

    public CheckoutModel(
        IStripeService stripeService,
        IOptions<PaymentOptions> options,
        UserManager<User> userManager)
    {
        _stripeService = stripeService;
        _options = options.Value;
        _userManager = userManager;
    }

    public bool IsSimulated => !_stripeService.IsConfigured;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string plan, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Challenge();
        }

        var planId = string.Equals(plan, "yearly", StringComparison.OrdinalIgnoreCase)
            ? _options.PlanIdYearly
            : _options.PlanIdMonthly;

        // Return-URLs für den Zahlungsanbieter: öffentliche Basis-URL
        // (Site__PublicBaseUrl oder Schema/Host inkl. X-Forwarded-Proto/Host).
        var baseUrl = HttpContext.GetPublicBaseUrl();
        var successUrl = baseUrl + Url.Page("/Membership/CheckoutComplete", null, null);
        var cancelUrl = baseUrl + Url.Page("/Unterstuetzer/Index", null, null);

        var session = await _stripeService.CreateSubscriptionCheckoutAsync(
            user.Id, user.Email, planId, successUrl, cancelUrl, cancellationToken);

        return Redirect(session.Url);
    }
}
