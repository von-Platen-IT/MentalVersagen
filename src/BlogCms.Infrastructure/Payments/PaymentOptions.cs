using System.Security.Cryptography;
using System.Text;

namespace BlogCms.Infrastructure.Payments;

/// <summary>
/// Payment configuration, bound from the "Stripe" configuration section
/// (see 04-Monetarisierung.md). When no secret key is set, the app uses a
/// local fake so the whole flow runs without external credentials.
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Stripe secret API key. Empty = use the development fake.</summary>
    public string? SecretKey { get; set; }

    public string? PublishableKey { get; set; }

    /// <summary>Signing secret used to verify incoming webhook signatures.</summary>
    public string WebhookSecret { get; set; } = "whsec_dev_secret";

    public string PlanIdMonthly { get; set; } = "price_monthly_dev";

    public string PlanIdYearly { get; set; } = "price_yearly_dev";

    /// <summary>Maximum accepted age of a webhook signature timestamp.</summary>
    public int WebhookToleranceSeconds { get; set; } = 300;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>
/// Verifies the "Stripe-Signature" header without the Stripe SDK, following the
/// documented scheme: signed payload is <c>{timestamp}.{body}</c> HMAC-SHA256.
/// </summary>
public static class StripeSignatureVerifier
{
    public static bool Verify(string json, string? signatureHeader, string secret, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            string.IsNullOrWhiteSpace(signatureHeader) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var segment in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = segment.Split('=', 2);
            if (pair.Length == 2)
            {
                parts[pair[0]] = pair[1];
            }
        }

        if (!parts.TryGetValue("t", out var timestampValue) ||
            !parts.TryGetValue("v1", out var providedSignature) ||
            !long.TryParse(timestampValue, out var timestamp))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > tolerance.TotalSeconds)
        {
            return false;
        }

        var expected = ComputeSignature(json, secret, timestamp);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant()));
    }

    public static string ComputeSignature(string json, string secret, long timestamp)
    {
        var payload = $"{timestamp}.{json}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildHeader(string json, string secret, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"t={ts},v1={ComputeSignature(json, secret, ts)}";
    }
}
