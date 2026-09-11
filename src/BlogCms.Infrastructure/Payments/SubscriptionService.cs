using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Payments;

public interface ISubscriptionService
{
    Task<Subscription> ActivateAsync(
        Guid userId, string planId, string? stripeCustomerId, string? stripeSubscriptionId,
        DateTime? currentPeriodEnd, SubscriptionStatus status, CancellationToken cancellationToken = default);

    Task HandleWebhookEventAsync(StripeWebhookEvent stripeEvent, CancellationToken cancellationToken = default);

    Task<Subscription?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Donation> RecordDonationAsync(
        decimal amount, string currency, PaymentProvider provider, string transactionId, Guid? userId,
        DonationStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies Stripe webhook events to the local <see cref="Subscription"/> records and
/// keeps the denormalized <see cref="User.SubscriptionStatus"/> cache in sync
/// (see 04-Monetarisierung.md).
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _db;

    public SubscriptionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Subscription> ActivateAsync(
        Guid userId, string planId, string? stripeCustomerId, string? stripeSubscriptionId,
        DateTime? currentPeriodEnd, SubscriptionStatus status, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            subscription = new Subscription { UserId = userId, CreatedAt = DateTime.UtcNow };
            _db.Subscriptions.Add(subscription);
        }

        subscription.PlanId = string.IsNullOrWhiteSpace(planId) ? subscription.PlanId : planId;
        subscription.StripeCustomerId = stripeCustomerId ?? subscription.StripeCustomerId;
        subscription.StripeSubscriptionId = stripeSubscriptionId ?? subscription.StripeSubscriptionId;
        subscription.Status = status;
        subscription.CurrentPeriodEnd = currentPeriodEnd ?? subscription.CurrentPeriodEnd;
        subscription.CanceledAt = status == SubscriptionStatus.Canceled ? DateTime.UtcNow : null;

        await _db.SaveChangesAsync(cancellationToken);
        await UpdateUserCacheAsync(userId, status, cancellationToken);

        return subscription;
    }

    public async Task HandleWebhookEventAsync(
        StripeWebhookEvent stripeEvent, CancellationToken cancellationToken = default)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.UserId is not null)
                {
                    await ActivateAsync(
                        stripeEvent.UserId.Value,
                        stripeEvent.PlanId ?? string.Empty,
                        stripeEvent.StripeCustomerId,
                        stripeEvent.StripeSubscriptionId,
                        stripeEvent.CurrentPeriodEnd,
                        SubscriptionStatus.Active,
                        cancellationToken);
                }
                break;

            case "invoice.paid":
            {
                var subscription = await FindByStripeIdAsync(stripeEvent, cancellationToken);
                if (subscription is not null)
                {
                    subscription.Status = SubscriptionStatus.Active;
                    subscription.CurrentPeriodEnd = stripeEvent.CurrentPeriodEnd ?? subscription.CurrentPeriodEnd;
                    await _db.SaveChangesAsync(cancellationToken);
                    await UpdateUserCacheAsync(subscription.UserId, SubscriptionStatus.Active, cancellationToken);
                }
                break;
            }

            case "customer.subscription.updated":
            {
                var subscription = await FindByStripeIdAsync(stripeEvent, cancellationToken);
                if (subscription is not null)
                {
                    var status = MapStatus(stripeEvent.SubscriptionStatus);
                    subscription.Status = status;
                    subscription.CurrentPeriodEnd = stripeEvent.CurrentPeriodEnd ?? subscription.CurrentPeriodEnd;
                    await _db.SaveChangesAsync(cancellationToken);
                    await UpdateUserCacheAsync(subscription.UserId, status, cancellationToken);
                }
                break;
            }

            case "customer.subscription.deleted":
            {
                var subscription = await FindByStripeIdAsync(stripeEvent, cancellationToken);
                if (subscription is not null)
                {
                    subscription.Status = SubscriptionStatus.Canceled;
                    subscription.CanceledAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    await UpdateUserCacheAsync(subscription.UserId, SubscriptionStatus.Canceled, cancellationToken);
                }
                break;
            }
        }
    }

    public Task<Subscription?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Donation> RecordDonationAsync(
        decimal amount, string currency, PaymentProvider provider, string transactionId, Guid? userId,
        DonationStatus status, CancellationToken cancellationToken = default)
    {
        var donation = new Donation
        {
            Amount = amount,
            Currency = currency,
            Provider = provider,
            ProviderTransactionId = transactionId,
            UserId = userId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _db.Donations.Add(donation);
        await _db.SaveChangesAsync(cancellationToken);
        return donation;
    }

    private Task<Subscription?> FindByStripeIdAsync(
        StripeWebhookEvent stripeEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stripeEvent.StripeSubscriptionId))
        {
            return Task.FromResult<Subscription?>(null);
        }

        return _db.Subscriptions.FirstOrDefaultAsync(
            s => s.StripeSubscriptionId == stripeEvent.StripeSubscriptionId, cancellationToken);
    }

    private async Task UpdateUserCacheAsync(
        Guid userId, SubscriptionStatus status, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.SubscriptionStatus = status switch
        {
            SubscriptionStatus.Active or SubscriptionStatus.Trialing => UserSubscriptionStatus.Active,
            SubscriptionStatus.PastDue => UserSubscriptionStatus.PastDue,
            SubscriptionStatus.Canceled => UserSubscriptionStatus.Canceled,
            _ => UserSubscriptionStatus.None
        };

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SubscriptionStatus MapStatus(string? status) => status switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" or "unpaid" or "incomplete_expired" => SubscriptionStatus.Canceled,
        _ => SubscriptionStatus.Active
    };
}
