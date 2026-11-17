using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Membership;

/// <summary>
/// Development fake used when no Stripe secret key is configured. It lets the
/// checkout, portal and webhook flow run locally without contacting Stripe.
/// Webhook signatures are not verified in this mode.
/// </summary>
public sealed class FakeStripeMembershipService : IStripeMembershipService
{
    private readonly MembershipOptions _options;

    public FakeStripeMembershipService(IOptions<MembershipOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => false;

    public Task<MembershipCheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MembershipCheckoutSession(successUrl, "fake_checkout_session"));

    public Task<string> CreatePortalSessionAsync(
        string stripeCustomerId, string returnUrl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(returnUrl);

    public MembershipStripeEvent? VerifyAndParseWebhook(string json, string? signatureHeader) =>
        MembershipStripeEventParser.ParseWithoutSignature(json);

    public Task<MembershipStripeSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MembershipStripeSubscription?>(null);
}