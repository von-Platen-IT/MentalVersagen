namespace BlogCms.Infrastructure.Membership;

/// <summary>
/// Configuration for the new Stripe membership feature, bound from the
/// "Membership" section. The feature is disabled by default so the existing
/// blog behaviour is unchanged until it is explicitly enabled.
/// </summary>
public sealed class MembershipOptions
{
    public const string SectionName = "Membership";

    public bool Enabled { get; set; }

    /// <summary>Stripe secret key. Must come from user secrets/environment, never from the repo.</summary>
    public string? SecretKey { get; set; }

    public string? PublishableKey { get; set; }

    /// <summary>Signing secret for the new /webhooks/stripe endpoint.</summary>
    public string? WebhookSecret { get; set; }

    public string PriceIdMonthly { get; set; } = string.Empty;

    public string PriceIdYearly { get; set; } = string.Empty;

    /// <summary>Days after publication before a premium article becomes free for everyone.</summary>
    public int TeaserDelayDays { get; set; } = 7;

    /// <summary>Grace period after the end of the billing period.</summary>
    public int GracePeriodDays { get; set; } = 3;

    /// <summary>Login sync runs when the last sync is older than this many hours.</summary>
    public int SyncOnLoginIfOlderThanHours { get; set; } = 24;

    /// <summary>Interval of the background synchronisation job.</summary>
    public int SyncIntervalHours { get; set; } = 24;

    public string CheckoutSuccessPath { get; set; } = "/Membership/CheckoutComplete";

    public string CheckoutCancelPath { get; set; } = "/Unterstuetzer/Index";

    public string PortalReturnPath { get; set; } = "/Account/Manage";
}