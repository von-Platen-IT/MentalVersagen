using System.Text.Json;

namespace BlogCms.Infrastructure.Payments;

/// <summary>
/// Parses the subset of Stripe webhook payload fields the app needs, shared by
/// the fake and the real Stripe service.
/// </summary>
public static class StripeEventParser
{
    public static StripeWebhookEvent? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeProperty))
            {
                return null;
            }

            var type = typeProperty.GetString() ?? string.Empty;
            JsonElement obj = default;
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("object", out var inner))
            {
                obj = inner;
            }

            return new StripeWebhookEvent(
                Type: type,
                UserId: ReadGuid(obj, "client_reference_id") ?? ReadMetadataGuid(obj, "userId"),
                StripeCustomerId: ReadString(obj, "customer"),
                StripeSubscriptionId: ReadString(obj, "subscription") ?? ReadString(obj, "id"),
                PlanId: ReadMetadataString(obj, "planId"),
                SubscriptionStatus: ReadString(obj, "status"),
                CurrentPeriodEnd: ReadUnixDate(obj, "current_period_end"),
                Amount: ReadAmount(obj),
                Currency: ReadString(obj, "currency"),
                TransactionId: ReadString(obj, "payment_intent") ?? ReadString(obj, "id"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ReadString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object &&
           obj.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? ReadGuid(JsonElement obj, string name)
        => Guid.TryParse(ReadString(obj, name), out var id) ? id : null;

    private static string? ReadMetadataString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty("metadata", out var metadata)
            ? ReadString(metadata, name)
            : null;

    private static Guid? ReadMetadataGuid(JsonElement obj, string name)
        => Guid.TryParse(ReadMetadataString(obj, name), out var id) ? id : null;

    private static DateTime? ReadUnixDate(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object &&
           obj.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    private static decimal? ReadAmount(JsonElement obj)
    {
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty("amount_total", out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var cents))
        {
            return cents / 100m;
        }

        return null;
    }
}
