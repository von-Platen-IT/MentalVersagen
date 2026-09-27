using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogCms.Tests;

public class ActivityLogTests
{
    private static Article NewArticle(string title) => new()
    {
        Title = title,
        ContentMarkdown = "Inhalt",
        Category = ArticleCategory.Politik,
        Status = ArticleStatus.Published,
        PublishedAt = DateTime.UtcNow,
        AuthorId = Guid.NewGuid()
    };

    [Fact]
    public async Task LogArticleView_StoresEntryWithIpAndUser()
    {
        var db = TestDb.Create();
        var article = NewArticle("A");
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var service = new ActivityLogService(db);
        var userId = Guid.NewGuid();

        await service.LogArticleViewAsync(article.Id, userId, "203.0.113.7");

        var entry = await db.ActivityLogEntries.SingleAsync();
        Assert.Equal(ActivityEventType.ArticleViewed, entry.EventType);
        Assert.Equal("203.0.113.7", entry.IpAddress);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(article.Id, entry.ArticleId);
    }

    [Fact]
    public async Task LogArticleView_Anonymous_StoresNullUserAndBlankIpAsNull()
    {
        var db = TestDb.Create();
        var service = new ActivityLogService(db);

        await service.LogArticleViewAsync(Guid.NewGuid(), null, "   ");

        var entry = await db.ActivityLogEntries.SingleAsync();
        Assert.Null(entry.UserId);
        Assert.Null(entry.IpAddress);
    }

    [Fact]
    public async Task GetOverview_AggregatesAllEventTypes()
    {
        var db = TestDb.Create();
        var article = NewArticle("Top");
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var service = new ActivityLogService(db);
        await service.LogArticleViewAsync(article.Id, null, "1.1.1.1");
        await service.LogArticleViewAsync(article.Id, null, "1.1.1.2");
        await service.LogArticleCreatedAsync(article.Id, Guid.NewGuid(), "1.1.1.3");
        await service.LogCommentCreatedAsync(article.Id, Guid.NewGuid(), Guid.NewGuid(), "1.1.1.4");
        await service.LogArticleRatedAsync(article.Id, RatingValue.ThumbUp, Guid.NewGuid(), "1.1.1.5");
        await service.LogArticleRatedAsync(article.Id, RatingValue.ThumbDown, Guid.NewGuid(), "1.1.1.6");

        var overview = await service.GetOverviewAsync(
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.Equal(2, overview.TotalViews);
        Assert.Equal(1, overview.TotalArticlesCreated);
        Assert.Equal(1, overview.TotalComments);
        Assert.Equal(1, overview.TotalThumbUp);
        Assert.Equal(1, overview.TotalThumbDown);

        var top = Assert.Single(overview.TopViewedArticles);
        Assert.Equal("Top", top.Title);
        Assert.Equal(2, top.Count);

        var rating = Assert.Single(overview.ArticleRatings);
        Assert.Equal(1, rating.ThumbUp);
        Assert.Equal(1, rating.ThumbDown);

        Assert.Single(overview.Daily);
        Assert.Equal(6, overview.RecentEntries.Count);
    }

    [Fact]
    public async Task GetOverview_ExcludesEventsOutsideRange()
    {
        var db = TestDb.Create();
        var service = new ActivityLogService(db);

        await service.LogArticleViewAsync(Guid.NewGuid(), null, "1.1.1.1");

        // A range entirely in the past must not include the just-recorded event.
        var overview = await service.GetOverviewAsync(
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-5));

        Assert.Equal(0, overview.TotalViews);
        Assert.Empty(overview.RecentEntries);
    }
}
