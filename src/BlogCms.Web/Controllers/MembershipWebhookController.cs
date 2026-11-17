using BlogCms.Infrastructure.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Stripe webhook endpoint for the new membership feature. The existing
/// /api/stripe/webhook endpoint is left untouched.
/// </summary>
[ApiController]
[Route("webhooks")]
public sealed class MembershipWebhookController : ControllerBase
{
    private readonly IStripeMembershipService _stripe;
    private readonly IMembershipWebhookHandler _handler;
    private readonly MembershipOptions _options;
    private readonly ILogger<MembershipWebhookController> _logger;

    public MembershipWebhookController(
        IStripeMembershipService stripe,
        IMembershipWebhookHandler handler,
        IOptions<MembershipOptions> options,
        ILogger<MembershipWebhookController> logger)
    {
        _stripe = stripe;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> Stripe(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var stripeEvent = _stripe.VerifyAndParseWebhook(json, signature);

        if (stripeEvent is null)
        {
            _logger.LogWarning(
                "Rejected membership Stripe webhook with an invalid signature or payload.");
            return BadRequest();
        }

        await _handler.HandleAsync(stripeEvent, cancellationToken);
        return Ok();
    }
}