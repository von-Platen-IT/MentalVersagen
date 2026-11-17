using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlogCms.Infrastructure.Membership;

/// <summary>
/// Extracts the membership-relevant fields from a Stripe webhook payload. The
/// signature is verified separately (Stripe.net or the development fake); this
/// parser only reads the reduced event shape.
/// </summary>
internal static class MembershipStripeEventParser
{
    public static MembershipStripeEvent? Parse(string eventId, string type, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("object", out var obj))
            {
                return null;
            }

            var userId = ReadGuid(obj, "client_reference_id") ?? ReadMetadataGuid(obj, "userId");
            var subscriptionId = ReadString(obj, "subscription")
                ?? (type.StartsWith("customer.subscription.", StringComparison.Ordinal)
                    ? ReadString(obj, "id")
                    : null);
            var customerId = ReadString(obj, "customer") ?? ReadString(obj, "customer_id");
            var status = ReadString(obj, "status");
            var currentPeriodEnd = ReadUnixDate(obj, "current_period_end");
            var cancelAtPeriodEnd = ReadBool(obj, "cancel_at_period_end");

            return new MembershipStripeEvent(
                eventId,
                type,
                userId,
                customerId,
                subscriptionId,
                status,
                currentPeriodEnd,
                cancelAtPeriodEnd,
                Hash(json),
                ReadPlanId(obj));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Development fake: parses without a signature.</summary>
    public static MembershipStripeEvent? ParseWithoutSignature(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = ReadString(root, "type") ?? "unknown";
            var id = ReadString(root, "id") ?? $"evt_fake_{Guid.NewGuid():N}";
            return Parse(id, type, json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? ReadGuid(JsonElement obj, string name) =>
        Guid.TryParse(ReadString(obj, name), out var id) ? id : null;

    private static string? ReadMetadataString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty("metadata", out var metadata)
            ? ReadString(metadata, name)
            : null;

    private static Guid? ReadMetadataGuid(JsonElement obj, string name) =>
        Guid.TryParse(ReadMetadataString(obj, name), out var id) ? id : null;

    private static DateTime? ReadUnixDate(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    private static bool ReadBool(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static string? ReadPlanId(JsonElement obj)
    {
        var metadataPlan = ReadMetadataString(obj, "planId");
        if (!string.IsNullOrWhiteSpace(metadataPlan))
        {
            return metadataPlan;
        }

        // Stripe subscription objects expose the price on the first item.
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty("items", out var items) &&
            items.ValueKind == JsonValueKind.Object &&
            items.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array &&
            data.GetArrayLength() > 0 &&
            data[0].TryGetProperty("price", out var price))
        {
            return ReadString(price, "id");
        }

        return null;
    }

    private static string Hash(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}