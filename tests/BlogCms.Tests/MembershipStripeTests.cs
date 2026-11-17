using System.Security.Claims;
using System.Text;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Membership;
using BlogCms.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Acceptance tests T07–T09 for the new Stripe membership endpoints.
/// </summary>
public class MembershipStripeTests
{
    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"stripe-{Guid.NewGuid():N}@example.com",
        Email = $"stripe-{Guid.NewGuid():N}@example.com",
        DisplayName = "Stripe Test",
        Role = UserRole.Reader
    };

    private static UserManager<User> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<User, IdentityRole<Guid>, AppDbContext, Guid>(db);
        return new UserManager<User>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<User>>.Instance);
    }

    private static HttpContext CreateHttpContext(Guid userId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        return context;
    }

    [Fact]
    public async Task T07_Checkout_MapsUserPlanAndReturnUrls()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService();
        var membership = new MembershipService(
            db, Options.Create(new MembershipOptions { Enabled = true }));
        var options = new MembershipOptions
        {
            Enabled = true,
            PriceIdMonthly = "price_month",
            PriceIdYearly = "price_year",
            CheckoutSuccessPath = "/Membership/CheckoutComplete",
            CheckoutCancelPath = "/Unterstuetzer/Index"
        };

        var controller = new MembershipController(
            stripe, membership, CreateUserManager(db), Options.Create(options))
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(user.Id) }
        };

        var result = await controller.Checkout("monthly", default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://checkout.example/session", redirect.Url);
        Assert.Equal(user.Id, stripe.LastUserId);
        Assert.Equal(user.Email, stripe.LastEmail);
        Assert.Equal("price_month", stripe.LastPlanId);
        Assert.Equal("https://example.test/Membership/CheckoutComplete", stripe.LastSuccessUrl);
        Assert.Equal("https://example.test/Unterstuetzer/Index", stripe.LastCancelUrl);
    }

    [Fact]
    public async Task T08_Webhook_InvalidSignature_ReturnsBadRequest()
    {
        var stripe = new StubStripeMembershipService { WebhookResult = null };
        var handler = new StubMembershipWebhookHandler();
        var controller = new MembershipWebhookController(
            stripe,
            handler,
            Options.Create(new MembershipOptions { Enabled = true, WebhookSecret = "whsec_test" }),
            NullLogger<MembershipWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        controller.HttpContext.Request.Body =
            new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        controller.HttpContext.Request.Headers["Stripe-Signature"] = "invalid";

        var result = await controller.Stripe(default);

        Assert.IsType<BadRequestResult>(result);
        Assert.False(handler.Called);
    }

    [Fact]
    public async Task T09_CheckoutSessionCompleted_ActivatesProfileOnce()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var membership = new MembershipService(
            db, Options.Create(new MembershipOptions { Enabled = true }));
        var handler = new MembershipWebhookHandler(
            db, membership, NullLogger<MembershipWebhookHandler>.Instance);

        var stripeEvent = new MembershipStripeEvent(
            "evt_test_checkout_completed",
            "checkout.session.completed",
            user.Id,
            "cus_test",
            "sub_test",
            "active",
            DateTime.UtcNow.AddDays(30),
            false,
            "payload-hash");

        await handler.HandleAsync(stripeEvent);
        await handler.HandleAsync(stripeEvent); // idempotency

        var profile = await db.MembershipProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(MembershipStatus.Active, profile.Status);
        Assert.Equal("cus_test", profile.StripeCustomerId);
        Assert.Equal("sub_test", profile.StripeSubscriptionId);
        Assert.NotNull(profile.CurrentPeriodEnd);
        Assert.Equal(1, await db.StripeEvents.CountAsync());
    }

    private sealed class StubStripeMembershipService : IStripeMembershipService
    {
        public bool IsConfigured => true;

        public Guid? LastUserId { get; private set; }
        public string? LastEmail { get; private set; }
        public string? LastPlanId { get; private set; }
        public string? LastSuccessUrl { get; private set; }
        public string? LastCancelUrl { get; private set; }

        public MembershipStripeEvent? WebhookResult { get; set; }

        public Task<MembershipCheckoutSession> CreateSubscriptionCheckoutAsync(
            Guid userId, string email, string planId, string successUrl, string cancelUrl,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastEmail = email;
            LastPlanId = planId;
            LastSuccessUrl = successUrl;
            LastCancelUrl = cancelUrl;

            return Task.FromResult(
                new MembershipCheckoutSession("https://checkout.example/session", "cs_test"));
        }

        public Task<string> CreatePortalSessionAsync(
            string stripeCustomerId, string returnUrl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(returnUrl);

        public MembershipStripeEvent? VerifyAndParseWebhook(
            string json, string? signatureHeader) => WebhookResult;

        public Task<MembershipStripeSubscription?> GetSubscriptionAsync(
            string subscriptionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MembershipStripeSubscription?>(null);
    }

    private sealed class StubMembershipWebhookHandler : IMembershipWebhookHandler
    {
        public bool Called { get; private set; }

        public Task HandleAsync(
            MembershipStripeEvent stripeEvent, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }
}