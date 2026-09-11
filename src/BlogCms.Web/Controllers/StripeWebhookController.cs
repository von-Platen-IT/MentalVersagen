using BlogCms.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Receives Stripe webhooks. The signature is verified before any processing,
/// so forged payment confirmations are rejected
/// (see 04-Monetarisierung.md).
/// </summary>
[ApiController]
[Route("api/stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IStripeService stripeService,
        ISubscriptionService subscriptionService,
        ILogger<StripeWebhookController> logger)
    {
        _stripeService = stripeService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (!_stripeService.VerifyWebhookSignature(json, signature))
        {
            _logger.LogWarning("Rejected Stripe webhook with invalid signature.");
            return Unauthorized();
        }

        var stripeEvent = _stripeService.ParseWebhookEvent(json);
        if (stripeEvent is null)
        {
            return BadRequest();
        }

        await _subscriptionService.HandleWebhookEventAsync(stripeEvent, cancellationToken);
        return Ok();
    }
}
