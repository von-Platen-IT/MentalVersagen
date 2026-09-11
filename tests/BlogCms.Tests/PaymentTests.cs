using BlogCms.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class StripeSignatureVerifierTests
{
    private const string Secret = "whsec_test_secret";

    [Fact]
    public void Verify_AcceptsValidSignature()
    {
        // Acceptance criterion: a correctly signed webhook is processed.
        var json = """{"type":"checkout.session.completed"}""";
        var header = StripeSignatureVerifier.BuildHeader(json, Secret);

        Assert.True(StripeSignatureVerifier.Verify(json, header, Secret, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Verify_RejectsTamperedPayload()
    {
        // Acceptance criterion: invalid signature is rejected and not processed.
        var json = """{"type":"checkout.session.completed"}""";
        var header = StripeSignatureVerifier.BuildHeader(json, Secret);

        Assert.False(StripeSignatureVerifier.Verify(
            """{"type":"checkout.session.completed","amount":999999}""", header, Secret, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Verify_RejectsExpiredTimestamp()
    {
        var json = """{"type":"invoice.paid"}""";
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var header = StripeSignatureVerifier.BuildHeader(json, Secret, oldTimestamp);

        Assert.False(StripeSignatureVerifier.Verify(json, header, Secret, TimeSpan.FromMinutes(5)));
    }
}

public class SubscriptionServiceTests
{
    [Fact]
    public async Task HandleWebhook_CheckoutCompleted_ActivatesSubscriptionAndCache()
    {
        var db = TestDb.Create();
        var user = new BlogCms.Domain.Entities.User { Id = Guid.NewGuid(), Email = "u@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);

        await service.HandleWebhookEventAsync(new StripeWebhookEvent(
            Type: "checkout.session.completed",
            UserId: user.Id,
            StripeCustomerId: "cus_1",
            StripeSubscriptionId: "sub_1",
            PlanId: "price_monthly",
            CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

        var subscription = await service.GetForUserAsync(user.Id);
        Assert.NotNull(subscription);
        Assert.Equal(BlogCms.Domain.Enums.SubscriptionStatus.Active, subscription!.Status);

        var cachedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal(BlogCms.Domain.Enums.UserSubscriptionStatus.Active, cachedUser!.SubscriptionStatus);
    }

    [Fact]
    public async Task HandleWebhook_SubscriptionDeleted_RevokesPremiumCache()
    {
        var db = TestDb.Create();
        var user = new BlogCms.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            SubscriptionStatus = BlogCms.Domain.Enums.UserSubscriptionStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        await service.ActivateAsync(user.Id, "price_monthly", "cus_1", "sub_1", DateTime.UtcNow.AddMonths(1),
            BlogCms.Domain.Enums.SubscriptionStatus.Active);

        await service.HandleWebhookEventAsync(new StripeWebhookEvent(
            Type: "customer.subscription.deleted",
            StripeSubscriptionId: "sub_1",
            SubscriptionStatus: "canceled"));

        var cachedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal(BlogCms.Domain.Enums.UserSubscriptionStatus.Canceled, cachedUser!.SubscriptionStatus);
    }

    [Fact]
    public async Task RecordDonation_AllowsAnonymousDonations()
    {
        // Acceptance criterion: donation without login stored with UserId = null.
        var db = TestDb.Create();
        var service = new SubscriptionService(db);

        var donation = await service.RecordDonationAsync(
            25m, "EUR", BlogCms.Domain.Enums.PaymentProvider.Stripe, "pi_1", null,
            BlogCms.Domain.Enums.DonationStatus.Completed);

        Assert.Null(donation.UserId);
        Assert.Equal(25m, donation.Amount);
    }
}

public class FakeStripeServiceTests
{
    [Fact]
    public async Task CreateSubscriptionCheckout_ReturnsSimulatedUrl()
    {
        var service = new FakeStripeService(Options.Create(new PaymentOptions()));

        var session = await service.CreateSubscriptionCheckoutAsync(
            Guid.NewGuid(), "u@example.com", "price_monthly",
            "https://localhost/Membership/CheckoutComplete", "https://localhost/Membership");

        Assert.Contains("simulated=1", session.Url);
        Assert.False(service.IsConfigured);
    }
}
