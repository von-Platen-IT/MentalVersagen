using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Membership;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Acceptance tests T05/T06 for admin-granted complimentary access.
/// </summary>
public class MembershipAdminTests
{
    private static MembershipService CreateService(AppDbContext db) =>
        new(db, Options.Create(new MembershipOptions
        {
            Enabled = true,
            TeaserDelayDays = 7,
            GracePeriodDays = 3
        }));

    private static User CreateUser(UserRole role = UserRole.Reader) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"admin-test-{Guid.NewGuid():N}@example.com",
        Email = $"admin-test-{Guid.NewGuid():N}@example.com",
        DisplayName = $"Admin Test {Guid.NewGuid():N}",
        Role = role
    };

    [Fact]
    public async Task T05_AdminGrantWithExpiry_ControlsAccess()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        var admin = CreateUser(UserRole.Admin);
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var until = DateTime.UtcNow.AddDays(10);

        var grant = await service.GrantComplimentaryAsync(
            user.Id, admin.Id, until, "Kulanzfreigabe");

        Assert.True(grant.Succeeded);
        Assert.True(await service.HasFullAccessAsync(user, until.AddDays(-1)));
        Assert.False(await service.HasFullAccessAsync(user, until.AddDays(1)));
    }

    [Fact]
    public async Task ApplyStripeSnapshot_SavesPlanId()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var snapshot = new MembershipStripeSnapshot(
            user.Id,
            "cus_plan",
            "sub_plan",
            MembershipStatus.Active,
            DateTime.UtcNow.AddDays(30),
            CancelAtPeriodEnd: false,
            LastSyncedAt: DateTime.UtcNow,
            PlanId: "price_month");

        var result = await service.ApplyStripeSnapshotAsync(
            snapshot, "Webhook", actorId: null, details: "test");

        Assert.True(result.Succeeded);
        var profile = await db.MembershipProfiles
            .SingleAsync(p => p.UserId == user.Id);
        Assert.Equal("price_month", profile.PlanId);
    }

    [Fact]
    public async Task GetForAdminAsync_FiltersPastDueAndCanceled()
    {
        await using var db = TestDb.Create();
        var pastDue = CreateUser();
        var canceled = CreateUser();
        db.Users.AddRange(pastDue, canceled);
        db.MembershipProfiles.AddRange(
            new MembershipProfile
            {
                UserId = pastDue.Id,
                Status = MembershipStatus.PastDue
            },
            new MembershipProfile
            {
                UserId = canceled.Id,
                Status = MembershipStatus.Canceled
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var pastDueRows = await service.GetForAdminAsync(null, MembershipFilter.PastDue);
        var canceledRows = await service.GetForAdminAsync(null, MembershipFilter.Canceled);

        Assert.Single(pastDueRows);
        Assert.Equal(pastDue.Id, pastDueRows[0].User.Id);
        Assert.Single(canceledRows);
        Assert.Equal(canceled.Id, canceledRows[0].User.Id);
    }

    [Fact]
    public async Task T06_AdminRevoke_RemovesAccessAndWritesAuditLog()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        var admin = CreateUser(UserRole.Admin);
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var grant = await service.GrantComplimentaryAsync(
            user.Id, admin.Id, DateTime.UtcNow.AddDays(10), "Kulanzfreigabe");
        Assert.True(grant.Succeeded);

        var revoke = await service.RevokeComplimentaryAsync(
            user.Id, admin.Id, "Freigabe beendet");

        Assert.True(revoke.Succeeded);
        Assert.False(await service.HasFullAccessAsync(user, DateTime.UtcNow));

        var audit = await db.MembershipAuditLogs
            .OrderBy(l => l.CreatedAt)
            .LastAsync();
        Assert.Equal("Revoke", audit.Action);
        Assert.Equal(user.Id, audit.TargetUserId);
        Assert.Equal(admin.Id, audit.ActorUserId);
        Assert.Contains("Freigabe beendet", audit.Details);
    }
}