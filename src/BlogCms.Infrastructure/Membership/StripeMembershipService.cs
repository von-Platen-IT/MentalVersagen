using Microsoft.Extensions.Options;
using Stripe;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using PortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using PortalSessionService = Stripe.BillingPortal.SessionService;
using StripeSubscriptionService = Stripe.SubscriptionService;

namespace BlogCms.Infrastructure.Membership;

/// <summary>
/// Stripe.net implementation for the new membership feature. It is selected when
/// <see cref="MembershipOptions.SecretKey"/> is configured. The existing raw-REST
/// payment flow is not touched.
/// </summary>
public sealed class StripeMembershipService : IStripeMembershipService
{
    private readonly MembershipOptions _options;

    public StripeMembershipService(IOptions<MembershipOptions> options)
    {
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            StripeConfiguration.ApiKey = _options.SecretKey;
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<MembershipCheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var service = new CheckoutSessionService();
        var session = await service.CreateAsync(new CheckoutSessionCreateOptions
        {
            Mode = "subscription",
            LineItems =
            [
                new CheckoutSessionLineItemOptions
                {
                    Price = planId,
                    Quantity = 1
                }
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = userId.ToString(),
            CustomerEmail = email,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["planId"] = planId
            }
        }, cancellationToken: cancellationToken);

        return new MembershipCheckoutSession(session.Url, session.Id);
    }

    public async Task<string> CreatePortalSessionAsync(
        string stripeCustomerId, string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var service = new PortalSessionService();
        var session = await service.CreateAsync(new PortalSessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl
        }, cancellationToken: cancellationToken);

        return session.Url;
    }

    public MembershipStripeEvent? VerifyAndParseWebhook(string json, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            string.IsNullOrWhiteSpace(signatureHeader) ||
            string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return null;
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                _options.WebhookSecret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);

            return MembershipStripeEventParser.Parse(stripeEvent.Id, stripeEvent.Type, json);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<MembershipStripeSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var service = new StripeSubscriptionService();
            var subscription = await service.GetAsync(
                subscriptionId, cancellationToken: cancellationToken);

            // Stripe.net 53 exposes the billing period on the subscription items.
            var periodEnds = subscription.Items?.Data?
                .Select(item => (DateTime?)item.CurrentPeriodEnd)
                .Where(value => value.HasValue)
                .ToList() ?? [];

            var periodEnd = periodEnds.Count > 0 ? periodEnds.Max() : null;

            var planId = subscription.Items?.Data?
                .FirstOrDefault()?.Price?.Id;

            return new MembershipStripeSubscription(
                subscription.Id,
                subscription.CustomerId,
                subscription.Status,
                periodEnd,
                subscription.CancelAtPeriodEnd,
                planId);
        }
        catch (StripeException)
        {
            return null;
        }
    }
}