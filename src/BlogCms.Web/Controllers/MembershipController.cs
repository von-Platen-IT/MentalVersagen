using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Membership;
using BlogCms.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Endpoints for the Stripe membership feature. <c>MembershipProfile</c> is the
/// single subscription source; the legacy /Membership/Checkout flow was removed.
/// </summary>
[Route("membership")]
public sealed class MembershipController : Controller
{
    private readonly IStripeMembershipService _stripe;
    private readonly IMembershipService _membership;
    private readonly UserManager<User> _userManager;
    private readonly MembershipOptions _options;

    public MembershipController(
        IStripeMembershipService stripe,
        IMembershipService membership,
        UserManager<User> userManager,
        IOptions<MembershipOptions> options)
    {
        _stripe = stripe;
        _membership = membership;
        _userManager = userManager;
        _options = options.Value;
    }

    [HttpPost("checkout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(
        string? plan, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Challenge();
        }

        var priceId = plan?.Trim().ToLowerInvariant() switch
        {
            "yearly" => _options.PriceIdYearly,
            "monthly" or null or "" => _options.PriceIdMonthly,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(priceId))
        {
            TempData["Error"] =
                "Für diesen Tarif ist keine Stripe-Preis-ID konfiguriert.";
            return Redirect(_options.CheckoutCancelPath);
        }

        var baseUrl = HttpContext.GetPublicBaseUrl();
        var successUrl = baseUrl + _options.CheckoutSuccessPath;
        var cancelUrl = baseUrl + _options.CheckoutCancelPath;

        var session = await _stripe.CreateSubscriptionCheckoutAsync(
            user.Id, user.Email, priceId, successUrl, cancelUrl, cancellationToken);

        return Redirect(session.Url);
    }

    [HttpPost("portal")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Portal(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var profile = await _membership.GetProfileAsync(user.Id, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.StripeCustomerId))
        {
            TempData["Error"] =
                "Für dieses Konto ist noch kein Stripe-Kunde hinterlegt.";
            return Redirect(_options.PortalReturnPath);
        }

        var baseUrl = HttpContext.GetPublicBaseUrl();
        var returnUrl = baseUrl + _options.PortalReturnPath;
        var url = await _stripe.CreatePortalSessionAsync(
            profile.StripeCustomerId, returnUrl, cancellationToken);

        return Redirect(url);
    }
}