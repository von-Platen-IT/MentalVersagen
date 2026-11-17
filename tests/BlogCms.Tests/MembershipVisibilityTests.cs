using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Membership;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Acceptance tests T01–T04 and T15 for the teaser/waiting-period rules.
/// </summary>
public class MembershipVisibilityTests
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

    private static Article PremiumArticle(DateTime publishedAt) => new()
    {
        Title = "Premium-Akte",
        ContentMarkdown = "Geheimer Volltext",
        AccessLevel = ArticleAccessLevel.Premium,
        PublishedAt = publishedAt,
        AuthorId = Guid.NewGuid()
    };

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"reader-{Guid.NewGuid():N}@example.com",
        Email = $"reader-{Guid.NewGuid():N}@example.com",
        DisplayName = "Testleser",
        Role = UserRole.Reader
    };

    [Fact]
    public async Task T01_FeatureDisabled_PremiumIsAdminOnly()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db, new MembershipOptions { Enabled = false });
        var article = PremiumArticle(DateTime.UtcNow);
        var reader = CreateUser();
        var admin = CreateUser();
        admin.Role = UserRole.Admin;

        Assert.False(await service.IsArticleFullyVisibleAsync(article, null, DateTime.UtcNow));
        Assert.False(await service.IsArticleFullyVisibleAsync(article, reader, DateTime.UtcNow));
        Assert.True(await service.IsArticleFullyVisibleAsync(article, admin, DateTime.UtcNow));
    }

    [Fact]
    public async Task T02_FeatureEnabled_AnonymousSeesTeaserBeforeDelay()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var publishedAt = DateTime.UtcNow.AddDays(-1);
        var article = PremiumArticle(publishedAt);

        Assert.False(await service.IsArticleFullyVisibleAsync(
            article, null, DateTime.UtcNow));

        var availableAt = service.GetTeaserAvailableAt(article);
        Assert.NotNull(availableAt);
        Assert.Equal(publishedAt.AddDays(7), availableAt);
    }

    [Fact]
    public async Task T03_FeatureEnabled_AnonymousSeesFullTextAfterDelay()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        Assert.True(await service.IsArticleFullyVisibleAsync(
            PremiumArticle(DateTime.UtcNow.AddDays(-8)), null, DateTime.UtcNow));
    }

    [Fact]
    public async Task T04_FeatureEnabled_RegisteredNonMemberSeesTeaser()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var user = CreateUser();

        Assert.False(await service.IsArticleFullyVisibleAsync(
            PremiumArticle(DateTime.UtcNow.AddDays(-1)), user, DateTime.UtcNow));
    }

    [Fact]
    public async Task T15_FeatureEnabled_ActiveMemberSeesFullText()
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

        Assert.True(await service.IsArticleFullyVisibleAsync(
            PremiumArticle(DateTime.UtcNow.AddDays(-1)), user, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetTeaserAvailableAt_IsNullForNonPremiumArticles()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var publicArticle = PremiumArticle(DateTime.UtcNow);
        publicArticle.AccessLevel = ArticleAccessLevel.Public;

        Assert.Null(service.GetTeaserAvailableAt(publicArticle));
    }
}