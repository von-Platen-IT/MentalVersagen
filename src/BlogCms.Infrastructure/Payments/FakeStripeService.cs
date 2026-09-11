using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Payments;

/// <summary>
/// Development implementation of <see cref="IStripeService"/>. Instead of calling
/// Stripe, it redirects back into the app with simulation parameters. Webhook
/// signature verification still uses the real HMAC scheme, so the webhook
/// endpoint (invalid-signature rejection) is fully functional and testable.
/// </summary>
public sealed class FakeStripeService : IStripeService
{
    private readonly PaymentOptions _options;

    public FakeStripeService(IOptions<PaymentOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => false;

    public Task<CheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var sessionId = $"cs_fake_{Guid.NewGuid():N}";
        var separator = successUrl.Contains('?') ? "&" : "?";
        var url = $"{successUrl}{separator}simulated=1&userId={userId}&planId={Uri.EscapeDataString(planId)}&session_id={sessionId}";
        return Task.FromResult(new CheckoutSession(url, sessionId));
    }

    public Task<CheckoutSession> CreateDonationCheckoutAsync(
        decimal amount, string currency, Guid? userId, string? email, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var sessionId = $"cs_fake_donation_{Guid.NewGuid():N}";
        var separator = successUrl.Contains('?') ? "&" : "?";
        var userPart = userId is null ? string.Empty : $"&userId={userId}";
        var url = $"{successUrl}{separator}simulated=1&amount={amount}&currency={currency}{userPart}&session_id={sessionId}";
        return Task.FromResult(new CheckoutSession(url, sessionId));
    }

    public string CreateCustomerPortalUrl(string? stripeCustomerId, string returnUrl) => returnUrl;

    public bool VerifyWebhookSignature(string json, string? signatureHeader)
        => StripeSignatureVerifier.Verify(
            json, signatureHeader, _options.WebhookSecret,
            TimeSpan.FromSeconds(_options.WebhookToleranceSeconds));

    public StripeWebhookEvent? ParseWebhookEvent(string json) => StripeEventParser.Parse(json);
}
