using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Membership;

/// <summary>Stripe synchronisation for the new membership feature.</summary>
public interface IMembershipSyncService
{
    /// <summary>Forces a Stripe refresh for one user; false if no subscription is known.</summary>
    Task<bool> SyncUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Refreshes only when <c>LastSyncedAt</c> is older than the configured threshold.</summary>
    Task SyncUserIfStaleAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Refreshes all known subscriptions in batches; returns the updated count.</summary>
    Task<int> SyncAllAsync(CancellationToken cancellationToken = default);
}

public sealed class MembershipSyncService : IMembershipSyncService
{
    private const int BatchSize = 50;

    private readonly AppDbContext _db;
    private readonly IStripeMembershipService _stripe;
    private readonly IMembershipService _membership;
    private readonly MembershipOptions _options;
    private readonly ILogger<MembershipSyncService> _logger;

    public MembershipSyncService(
        AppDbContext db,
        IStripeMembershipService stripe,
        IMembershipService membership,
        IOptions<MembershipOptions> options,
        ILogger<MembershipSyncService> logger)
    {
        _db = db;
        _stripe = stripe;
        _membership = membership;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SyncUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var profile = await _db.MembershipProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile is not null && await SyncProfileAsync(profile, cancellationToken);
    }

    public async Task SyncUserIfStaleAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var profile = await _db.MembershipProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null || string.IsNullOrWhiteSpace(profile.StripeSubscriptionId))
        {
            return;
        }

        var thresholdHours = Math.Max(1, _options.SyncOnLoginIfOlderThanHours);
        var threshold = DateTime.UtcNow.AddHours(-thresholdHours);

        if (profile.LastSyncedAt is not null && profile.LastSyncedAt >= threshold)
        {
            return;
        }

        await SyncUserAsync(userId, cancellationToken);
    }

    public async Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        var updated = 0;
        var offset = 0;

        while (true)
        {
            var batch = await _db.MembershipProfiles
                .Where(p => p.StripeSubscriptionId != null && p.StripeSubscriptionId != "")
                .OrderBy(p => p.Id)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var profile in batch)
            {
                if (await SyncProfileAsync(profile, cancellationToken))
                {
                    updated++;
                }
            }

            if (batch.Count < BatchSize)
            {
                break;
            }

            offset += batch.Count;
        }

        return updated;
    }

    private async Task<bool> SyncProfileAsync(
        MembershipProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.StripeSubscriptionId))
        {
            return false;
        }

        try
        {
            var subscription = await _stripe.GetSubscriptionAsync(
                profile.StripeSubscriptionId, cancellationToken);

            if (subscription is null)
            {
                // Keep the last known status; Stripe may be temporarily unavailable.
                _logger.LogWarning(
                    "Stripe subscription {SubscriptionId} could not be loaded; keeping last known status.",
                    profile.StripeSubscriptionId);
                return false;
            }

            var snapshot = new MembershipStripeSnapshot(
                profile.UserId,
                subscription.CustomerId,
                subscription.SubscriptionId,
                MapStatus(subscription.Status),
                subscription.CurrentPeriodEnd,
                subscription.CancelAtPeriodEnd,
                DateTime.UtcNow,
                subscription.PlanId);

            await _membership.ApplyStripeSnapshotAsync(
                snapshot,
                action: "Sync",
                actorId: null,
                details: "Stripe sync",
                cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Membership sync failed for user {UserId}; keeping last known status.",
                profile.UserId);
            return false;
        }
    }

    private static MembershipStatus MapStatus(string? status) => status switch
    {
        "active" => MembershipStatus.Active,
        "trialing" => MembershipStatus.Trialing,
        "past_due" => MembershipStatus.PastDue,
        "canceled" => MembershipStatus.Canceled,
        "unpaid" => MembershipStatus.Unpaid,
        "incomplete" => MembershipStatus.Incomplete,
        _ => MembershipStatus.None
    };
}