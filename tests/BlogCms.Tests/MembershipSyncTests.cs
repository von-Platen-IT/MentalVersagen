using System.Security.Claims;
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
/// Acceptance tests T10–T14 for portal, login sync and background sync.
/// </summary>
public class MembershipSyncTests
{
    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"sync-{Guid.NewGuid():N}@example.com",
        Email = $"sync-{Guid.NewGuid():N}@example.com",
        DisplayName = "Sync Test",
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

    private static MembershipSyncService CreateSyncService(
        AppDbContext db, IStripeMembershipService stripe, MembershipOptions options)
    {
        var membership = new MembershipService(db, Options.Create(options));
        return new MembershipSyncService(
            db,
            stripe,
            membership,
            Options.Create(options),
            NullLogger<MembershipSyncService>.Instance);
    }

    [Fact]
    public async Task T10_Portal_RedirectsWhenCustomerIdExists()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            StripeCustomerId = "cus_portal",
            Status = MembershipStatus.Active
        });
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService();
        var membership = new MembershipService(
            db, Options.Create(new MembershipOptions { Enabled = true }));
        var controller = new MembershipController(
            stripe,
            membership,
            CreateUserManager(db),
            Options.Create(new MembershipOptions
            {
                Enabled = true,
                PortalReturnPath = "/Account/Manage"
            }))
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(user.Id) }
        };

        var result = await controller.Portal(default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://portal.example/session", redirect.Url);
        Assert.Equal("cus_portal", stripe.LastPortalCustomerId);
        Assert.Equal("https://example.test/Account/Manage", stripe.LastPortalReturnUrl);
    }

    [Fact]
    public async Task T11_LoginSync_UpdatesStaleProfile()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            StripeCustomerId = "cus_sync",
            StripeSubscriptionId = "sub_sync",
            Status = MembershipStatus.Canceled,
            LastSyncedAt = DateTime.UtcNow.AddHours(-48)
        });
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService
        {
            SubscriptionResult = new MembershipStripeSubscription(
                "sub_sync", "cus_sync", "active", DateTime.UtcNow.AddDays(30), false)
        };
        var sync = CreateSyncService(db, stripe, new MembershipOptions
        {
            Enabled = true,
            SyncOnLoginIfOlderThanHours = 24
        });

        await sync.SyncUserIfStaleAsync(user.Id);

        var profile = await db.MembershipProfiles
            .SingleOrDefaultAsync(p => p.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal(MembershipStatus.Active, profile!.Status);
        Assert.NotNull(profile.LastSyncedAt);
        Assert.True(profile.LastSyncedAt > DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(1, stripe.GetSubscriptionCalls);
    }

    [Fact]
    public async Task T12_LoginSyncFailure_KeepsLastKnownStatus()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        var originalSync = DateTime.UtcNow.AddHours(-48);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            StripeCustomerId = "cus_sync",
            StripeSubscriptionId = "sub_sync",
            Status = MembershipStatus.Active,
            LastSyncedAt = originalSync
        });
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService { SubscriptionResult = null };
        var sync = CreateSyncService(db, stripe, new MembershipOptions
        {
            Enabled = true,
            SyncOnLoginIfOlderThanHours = 24
        });

        await sync.SyncUserIfStaleAsync(user.Id);

        var profile = await db.MembershipProfiles
            .SingleOrDefaultAsync(p => p.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal(MembershipStatus.Active, profile!.Status);
        Assert.Equal(originalSync, profile.LastSyncedAt);
    }

    [Fact]
    public async Task T13_BackgroundSync_UpdatesKnownSubscriptions()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            StripeCustomerId = "cus_sync",
            StripeSubscriptionId = "sub_sync",
            Status = MembershipStatus.Canceled,
            LastSyncedAt = DateTime.UtcNow.AddHours(-48)
        });
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService
        {
            SubscriptionResult = new MembershipStripeSubscription(
                "sub_sync", "cus_sync", "active", DateTime.UtcNow.AddDays(30), false)
        };
        var sync = CreateSyncService(db, stripe, new MembershipOptions { Enabled = true });

        var updated = await sync.SyncAllAsync();

        Assert.Equal(1, updated);
        var profile = await db.MembershipProfiles
            .SingleOrDefaultAsync(p => p.UserId == user.Id);
        Assert.Equal(MembershipStatus.Active, profile!.Status);
    }

    [Fact]
    public async Task T14_BackgroundSync_DoesNothingWhenDisabled()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            StripeCustomerId = "cus_sync",
            StripeSubscriptionId = "sub_sync",
            Status = MembershipStatus.Active
        });
        await db.SaveChangesAsync();

        var stripe = new StubStripeMembershipService();
        var sync = CreateSyncService(db, stripe, new MembershipOptions { Enabled = false });

        var updated = await sync.SyncAllAsync();

        Assert.Equal(0, updated);
        Assert.Equal(0, stripe.GetSubscriptionCalls);
    }

    private sealed class StubStripeMembershipService : IStripeMembershipService
    {
        public bool IsConfigured => true;

        public MembershipStripeSubscription? SubscriptionResult { get; set; }
        public int GetSubscriptionCalls { get; private set; }

        public string? LastPortalCustomerId { get; private set; }
        public string? LastPortalReturnUrl { get; private set; }

        public Task<MembershipCheckoutSession> CreateSubscriptionCheckoutAsync(
            Guid userId, string email, string planId, string successUrl, string cancelUrl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MembershipCheckoutSession(successUrl, "cs_test"));

        public Task<string> CreatePortalSessionAsync(
            string stripeCustomerId, string returnUrl,
            CancellationToken cancellationToken = default)
        {
            LastPortalCustomerId = stripeCustomerId;
            LastPortalReturnUrl = returnUrl;
            return Task.FromResult("https://portal.example/session");
        }

        public MembershipStripeEvent? VerifyAndParseWebhook(
            string json, string? signatureHeader) => null;

        public Task<MembershipStripeSubscription?> GetSubscriptionAsync(
            string subscriptionId, CancellationToken cancellationToken = default)
        {
            GetSubscriptionCalls++;
            return Task.FromResult(SubscriptionResult);
        }
    }
}