using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlogCms.Infrastructure.Membership;

/// <summary>Processes a verified membership Stripe event exactly once.</summary>
public interface IMembershipWebhookHandler
{
    Task HandleAsync(
        MembershipStripeEvent stripeEvent, CancellationToken cancellationToken = default);
}

public sealed class MembershipWebhookHandler : IMembershipWebhookHandler
{
    private readonly AppDbContext _db;
    private readonly IMembershipService _membership;
    private readonly ILogger<MembershipWebhookHandler> _logger;

    public MembershipWebhookHandler(
        AppDbContext db,
        IMembershipService membership,
        ILogger<MembershipWebhookHandler> logger)
    {
        _db = db;
        _membership = membership;
        _logger = logger;
    }

    public async Task HandleAsync(
        MembershipStripeEvent stripeEvent, CancellationToken cancellationToken = default)
    {
        // Idempotency: Stripe may retry the same event.
        var alreadyProcessed = await _db.StripeEvents
            .AnyAsync(e => e.StripeEventId == stripeEvent.EventId, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Membership Stripe event {EventId} was already processed.", stripeEvent.EventId);
            return;
        }

        var userId = await ResolveUserIdAsync(stripeEvent, cancellationToken);
        if (userId is null)
        {
            // The requirement is explicit: without a reliable user mapping no
            // status may be changed. Record the event so retries are idempotent.
            _logger.LogWarning(
                "Membership Stripe event {EventId} ({Type}) has no user mapping.",
                stripeEvent.EventId, stripeEvent.Type);
            AddEvent(stripeEvent);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var snapshot = new MembershipStripeSnapshot(
            userId.Value,
            stripeEvent.StripeCustomerId,
            stripeEvent.StripeSubscriptionId,
            MapStatus(stripeEvent),
            stripeEvent.CurrentPeriodEnd,
            stripeEvent.CancelAtPeriodEnd,
            DateTime.UtcNow,
            stripeEvent.PlanId);

        // Add the event first; the service's SaveChanges persists both the profile
        // and the idempotency record in one transaction.
        AddEvent(stripeEvent);
        await _membership.ApplyStripeSnapshotAsync(
            snapshot,
            action: "Webhook",
            actorId: null,
            details: stripeEvent.Type,
            cancellationToken);
    }

    private async Task<Guid?> ResolveUserIdAsync(
        MembershipStripeEvent stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.UserId is not null)
        {
            return stripeEvent.UserId;
        }

        if (!string.IsNullOrWhiteSpace(stripeEvent.StripeSubscriptionId))
        {
            var bySubscription = await _db.MembershipProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.StripeSubscriptionId == stripeEvent.StripeSubscriptionId,
                    cancellationToken);

            if (bySubscription is not null)
            {
                return bySubscription.UserId;
            }
        }

        if (!string.IsNullOrWhiteSpace(stripeEvent.StripeCustomerId))
        {
            var byCustomer = await _db.MembershipProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.StripeCustomerId == stripeEvent.StripeCustomerId,
                    cancellationToken);

            if (byCustomer is not null)
            {
                return byCustomer.UserId;
            }
        }

        return null;
    }

    private static MembershipStatus MapStatus(MembershipStripeEvent stripeEvent) =>
        stripeEvent.Type switch
        {
            "checkout.session.completed" => MembershipStatus.Active,
            "invoice.paid" => MembershipStatus.Active,
            "invoice.payment_failed" => MembershipStatus.PastDue,
            "customer.subscription.deleted" => MembershipStatus.Canceled,
            _ => stripeEvent.SubscriptionStatus switch
            {
                "active" => MembershipStatus.Active,
                "trialing" => MembershipStatus.Trialing,
                "past_due" => MembershipStatus.PastDue,
                "canceled" => MembershipStatus.Canceled,
                "unpaid" => MembershipStatus.Unpaid,
                "incomplete" => MembershipStatus.Incomplete,
                _ => MembershipStatus.None
            }
        };

    private void AddEvent(MembershipStripeEvent stripeEvent)
    {
        _db.StripeEvents.Add(new StripeEvent
        {
            StripeEventId = stripeEvent.EventId,
            Type = stripeEvent.Type,
            ProcessedAt = DateTime.UtcNow,
            PayloadHash = stripeEvent.PayloadHash
        });
    }
}