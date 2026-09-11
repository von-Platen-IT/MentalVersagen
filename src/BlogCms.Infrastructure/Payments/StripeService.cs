using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Payments;

/// <summary>
/// Real Stripe implementation using the REST API directly (Checkout Sessions and
/// Billing Portal). Activated automatically when a Stripe secret key is configured.
/// </summary>
public sealed class StripeService : IStripeService
{
    private const string ApiBase = "https://api.stripe.com/v1";

    private readonly PaymentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public StripeService(IOptions<PaymentOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<CheckoutSession> CreateSubscriptionCheckoutAsync(
        Guid userId, string email, string planId, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["line_items[0][price]"] = planId,
            ["line_items[0][quantity]"] = "1",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["client_reference_id"] = userId.ToString(),
            ["customer_email"] = email,
            ["metadata[userId]"] = userId.ToString(),
            ["metadata[planId]"] = planId
        };

        return await CreateSessionAsync(form, cancellationToken);
    }

    public async Task<CheckoutSession> CreateDonationCheckoutAsync(
        decimal amount, string currency, Guid? userId, string? email, string successUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var unitAmount = (long)Math.Round(amount * 100m);

        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["line_items[0][price_data][currency]"] = currency.ToLowerInvariant(),
            ["line_items[0][price_data][product_data][name]"] = "Spende",
            ["line_items[0][price_data][unit_amount]"] = unitAmount.ToString(),
            ["line_items[0][quantity]"] = "1",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl
        };

        if (userId is not null)
        {
            form["client_reference_id"] = userId.Value.ToString();
            form["metadata[userId]"] = userId.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            form["customer_email"] = email;
        }

        return await CreateSessionAsync(form, cancellationToken);
    }

    public string CreateCustomerPortalUrl(string? stripeCustomerId, string returnUrl)
    {
        // The hosted portal requires a known Stripe customer. Without one we fall
        // back to the membership page so the UI never breaks.
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            return returnUrl;
        }

        var form = new Dictionary<string, string>
        {
            ["customer"] = stripeCustomerId,
            ["return_url"] = returnUrl
        };

        try
        {
            var client = CreateClient();
            using var content = new FormUrlEncodedContent(form);
            using var response = client.PostAsync($"{ApiBase}/billing_portal/sessions", content)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return document.RootElement.GetProperty("url").GetString() ?? returnUrl;
        }
        catch (Exception)
        {
            return returnUrl;
        }
    }

    public bool VerifyWebhookSignature(string json, string? signatureHeader)
        => StripeSignatureVerifier.Verify(
            json, signatureHeader, _options.WebhookSecret,
            TimeSpan.FromSeconds(_options.WebhookToleranceSeconds));

    public StripeWebhookEvent? ParseWebhookEvent(string json) => StripeEventParser.Parse(json);

    private async Task<CheckoutSession> CreateSessionAsync(
        Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync($"{ApiBase}/checkout/sessions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var url = payload.GetProperty("url").GetString() ?? throw new InvalidOperationException("Stripe did not return a checkout URL.");
        var id = payload.GetProperty("id").GetString() ?? string.Empty;

        return new CheckoutSession(url, id);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("stripe");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        return client;
    }
}
