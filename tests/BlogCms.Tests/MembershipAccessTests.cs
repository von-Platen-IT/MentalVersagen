using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Membership;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Unit tests for the new membership access rules. The feature is disabled by
/// default, so every test explicitly opts in and sets the relevant periods.
/// </summary>
public class MembershipAccessTests
{
    private static MembershipOptions EnabledOptions() => new()
    {
        Enabled = true,
        TeaserDelayDays = 7,
        GracePeriodDays = 3
    };

    private static MembershipService CreateService(
        AppDbContext db, MembershipOptions? options = null) =>
        new(db, Options.Create(options ?? EnabledOptions()));

    private static User CreateUser(UserRole role = UserRole.Reader)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new User
        {
            Id = Guid.NewGuid(),
            UserName = $"member-{suffix}@example.com",
            Email = $"member-{suffix}@example.com",
            DisplayName = $"Member {suffix}",
            Role = role
        };
    }

    private static Article CreateArticle(
        ArticleAccessLevel accessLevel = ArticleAccessLevel.Premium,
        DateTime? publishedAt = null) => new()
    {
        Title = "Akte",
        ContentMarkdown = "Inhalt",
        AccessLevel = accessLevel,
        PublishedAt = publishedAt ?? DateTime.UtcNow,
        AuthorId = Guid.NewGuid()
    };

    [Fact]
    public async Task HasFullAccess_FeatureDisabled_OnlyAdmin()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db, new MembershipOptions { Enabled = false });
        var admin = CreateUser(UserRole.Admin);
        var reader = CreateUser();

        Assert.True(await service.HasFullAccessAsync(admin, DateTime.UtcNow));
        Assert.False(await service.HasFullAccessAsync(reader, DateTime.UtcNow));
        Assert.False(await service.HasFullAccessAsync(null, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_Anonymous_ReturnsFalse()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        Assert.False(await service.HasFullAccessAsync(null, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_Admin_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var admin = CreateUser(UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(admin, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_ActiveWithinGrace_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(user, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_ActiveAfterGrace_ReturnsFalse()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.False(await service.HasFullAccessAsync(user, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_CanceledWithinGrace_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.Canceled,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(user, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_CanceledAfterGrace_ReturnsFalse()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.Canceled,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.False(await service.HasFullAccessAsync(user, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_PastDueWithinGrace_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.PastDue,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(user, DateTime.UtcNow));
    }

    [Fact]
    public async Task HasFullAccess_ComplimentaryWithoutExpiry_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            IsComplimentary = true,
            ComplimentaryUntil = null
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(user, DateTime.UtcNow.AddYears(10)));
    }

    [Fact]
    public async Task HasFullAccess_ComplimentaryExpiry_IsRespected()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            IsComplimentary = true,
            ComplimentaryUntil = DateTime.UtcNow.AddDays(5)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.HasFullAccessAsync(user, DateTime.UtcNow));
        Assert.False(await service.HasFullAccessAsync(user, DateTime.UtcNow.AddDays(10)));
    }

    [Fact]
    public async Task IsArticleFullyVisible_FeatureDisabled_PublicRegisteredPremium()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db, new MembershipOptions { Enabled = false });
        var now = DateTime.UtcNow;
        var publicArticle = CreateArticle(ArticleAccessLevel.Public);
        var registeredArticle = CreateArticle(ArticleAccessLevel.Registered);
        var premiumArticle = CreateArticle(ArticleAccessLevel.Premium);
        var reader = CreateUser();
        var admin = CreateUser(UserRole.Admin);

        Assert.True(await service.IsArticleFullyVisibleAsync(publicArticle, null, now));
        Assert.False(await service.IsArticleFullyVisibleAsync(registeredArticle, null, now));
        Assert.True(await service.IsArticleFullyVisibleAsync(registeredArticle, reader, now));
        Assert.False(await service.IsArticleFullyVisibleAsync(premiumArticle, reader, now));
        Assert.True(await service.IsArticleFullyVisibleAsync(premiumArticle, admin, now));
    }

    [Fact]
    public async Task IsArticleFullyVisible_PublicAndRegistered_KeepTheirSemantics()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var now = DateTime.UtcNow;

        var publicArticle = CreateArticle(ArticleAccessLevel.Public);
        var registeredArticle = CreateArticle(ArticleAccessLevel.Registered);

        Assert.True(await service.IsArticleFullyVisibleAsync(publicArticle, null, now));
        Assert.False(await service.IsArticleFullyVisibleAsync(registeredArticle, null, now));

        var user = CreateUser();
        Assert.True(await service.IsArticleFullyVisibleAsync(registeredArticle, user, now));
    }

    [Fact]
    public async Task IsArticleFullyVisible_PremiumBeforeAndAfterTeaserDelay()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var now = DateTime.UtcNow;

        var fresh = CreateArticle(ArticleAccessLevel.Premium, now.AddDays(-1));
        var old = CreateArticle(ArticleAccessLevel.Premium, now.AddDays(-8));

        Assert.False(await service.IsArticleFullyVisibleAsync(fresh, null, now));
        Assert.True(await service.IsArticleFullyVisibleAsync(old, null, now));
    }

    [Fact]
    public async Task IsArticleFullyVisible_ActiveMember_ReturnsTrue()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        db.Users.Add(user);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = user.Id,
            Status = MembershipStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var article = CreateArticle(ArticleAccessLevel.Premium, DateTime.UtcNow.AddDays(-1));

        Assert.True(await service.IsArticleFullyVisibleAsync(
            article, user, DateTime.UtcNow));
    }

    [Fact]
    public async Task GrantAndRevokeComplimentary_WritesAuditLog()
    {
        await using var db = TestDb.Create();
        var user = CreateUser();
        var admin = CreateUser(UserRole.Admin);
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var grant = await service.GrantComplimentaryAsync(
            user.Id, admin.Id, DateTime.UtcNow.AddDays(30), "Testfreigabe");
        Assert.True(grant.Succeeded);
        Assert.True((await service.GetProfileAsync(user.Id))!.IsComplimentary);

        var revoke = await service.RevokeComplimentaryAsync(
            user.Id, admin.Id, "Testfreigabe beendet");
        Assert.True(revoke.Succeeded);
        Assert.False((await db.MembershipProfiles.SingleAsync()).IsComplimentary);

        var actions = await db.MembershipAuditLogs
            .OrderBy(l => l.CreatedAt)
            .Select(l => l.Action)
            .ToListAsync();
        Assert.Equal(["Grant", "Revoke"], actions);
    }

    [Fact]
    public async Task GetForAdminAsync_FiltersMembersAndBasicUsers()
    {
        await using var db = TestDb.Create();
        var member = CreateUser();
        var basic = CreateUser();
        db.Users.AddRange(member, basic);
        db.MembershipProfiles.Add(new MembershipProfile
        {
            UserId = member.Id,
            Status = MembershipStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var members = await service.GetForAdminAsync(null, MembershipFilter.Active);
        var basicUsers = await service.GetForAdminAsync(null, MembershipFilter.Basic);

        Assert.Single(members);
        Assert.Equal(member.Id, members[0].User.Id);
        Assert.Single(basicUsers);
        Assert.Equal(basic.Id, basicUsers[0].User.Id);
    }
}