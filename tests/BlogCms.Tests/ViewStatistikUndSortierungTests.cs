using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests für View-Statistik und Sortierung der Landing Page: Zähler-Inkrement,
/// die neuen Sortieroptionen (Views/Likes/Daumen auf- und absteigend) sowie die
/// pro-Artikel-Aufrufstatistik der Admin-Auswertung.
/// </summary>
public class ViewStatistikUndSortierungTests
{
    private static (ArticleService Articles, ActivityLogService Activity, AppDbContext Db) CreateServices()
    {
        var db = TestDb.Create();
        return (new ArticleService(db, new SlugGenerator()), new ActivityLogService(db), db);
    }

    private static async Task<Guid> CreateUserAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"autor-{suffix}@example.com",
            Email = $"autor-{suffix}@example.com",
            DisplayName = "Testautor"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> CreatePublishedArticleAsync(
        AppDbContext db, Guid authorId, string title, int viewCount = 0)
    {
        var article = new Article
        {
            Title = title,
            Slug = $"slug-{Guid.NewGuid():N}",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Politik,
            AccessLevel = ArticleAccessLevel.Public,
            Status = ArticleStatus.Published,
            PublishedAt = DateTime.UtcNow.AddMinutes(-5),
            AuthorId = authorId,
            ViewCount = viewCount
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    private static async Task AddRatingAsync(AppDbContext db, Guid articleId, RatingValue value)
    {
        db.Ratings.Add(new Rating
        {
            ArticleId = articleId,
            UserId = await CreateUserAsync(db),
            Value = value
        });
        await db.SaveChangesAsync();
    }

    // --- Zähler ---

    [Fact]
    public async Task LogArticleView_IncrementsViewCount()
    {
        var (articles, activity, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var articleId = await CreatePublishedArticleAsync(db, authorId, "Beitrag");

        await activity.LogArticleViewAsync(articleId, null, "127.0.0.1");
        await activity.LogArticleViewAsync(articleId, null, "127.0.0.1");

        var article = await db.Articles.FindAsync(articleId);
        Assert.Equal(2, article!.ViewCount);
    }

    // --- Sortierung ---

    [Fact]
    public async Task SearchPublished_MostViewed_OrdersByViewCountDescending()
    {
        var (articles, _, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var wenig = await CreatePublishedArticleAsync(db, authorId, "Wenig", viewCount: 1);
        var viel = await CreatePublishedArticleAsync(db, authorId, "Viel", viewCount: 9);

        var (items, _) = await articles.SearchPublishedAsync(
            new ArticleQuery(1, 10, Sort: ArticleSortOrder.MostViewed));

        Assert.Equal(viel, items[0].Id);
        Assert.Equal(wenig, items[1].Id);
    }

    [Fact]
    public async Task SearchPublished_LeastViewed_OrdersByViewCountAscending()
    {
        var (articles, _, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var wenig = await CreatePublishedArticleAsync(db, authorId, "Wenig", viewCount: 1);
        var viel = await CreatePublishedArticleAsync(db, authorId, "Viel", viewCount: 9);

        var (items, _) = await articles.SearchPublishedAsync(
            new ArticleQuery(1, 10, Sort: ArticleSortOrder.LeastViewed));

        Assert.Equal(wenig, items[0].Id);
        Assert.Equal(viel, items[1].Id);
    }

    [Fact]
    public async Task SearchPublished_LeastUpvoted_OrdersByThumbUpAscending()
    {
        var (articles, _, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var ohne = await CreatePublishedArticleAsync(db, authorId, "Ohne");
        var mit = await CreatePublishedArticleAsync(db, authorId, "Mit");
        await AddRatingAsync(db, mit, RatingValue.ThumbUp);

        var (items, _) = await articles.SearchPublishedAsync(
            new ArticleQuery(1, 10, Sort: ArticleSortOrder.LeastUpvoted));

        Assert.Equal(ohne, items[0].Id);
        Assert.Equal(mit, items[1].Id);
    }

    [Fact]
    public async Task SearchPublished_LeastDownvoted_OrdersByThumbDownAscending()
    {
        var (articles, _, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var ohne = await CreatePublishedArticleAsync(db, authorId, "Ohne");
        var mit = await CreatePublishedArticleAsync(db, authorId, "Mit");
        await AddRatingAsync(db, mit, RatingValue.ThumbDown);

        var (items, _) = await articles.SearchPublishedAsync(
            new ArticleQuery(1, 10, Sort: ArticleSortOrder.LeastDownvoted));

        Assert.Equal(ohne, items[0].Id);
        Assert.Equal(mit, items[1].Id);
    }

    // --- Admin-Verlauf: wie oft und wann ---

    [Fact]
    public async Task GetArticleViewHistory_ReturnsTotalsLastViewAndDailyCounts()
    {
        var (_, activity, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var articleId = await CreatePublishedArticleAsync(db, authorId, "Beitrag");

        await activity.LogArticleViewAsync(articleId, null, "127.0.0.1");
        await activity.LogArticleViewAsync(articleId, null, "127.0.0.1");

        var from = DateTime.UtcNow.Date.AddDays(-1);
        var to = DateTime.UtcNow.Date.AddDays(1);
        var history = await activity.GetArticleViewHistoryAsync(from, to);

        var entry = Assert.Single(history);
        Assert.Equal(articleId, entry.ArticleId);
        Assert.Equal("Beitrag", entry.Title);
        Assert.Equal(2, entry.OverallViews);
        Assert.Equal(2, entry.PeriodViews);
        Assert.NotNull(entry.LastViewedAt);

        var day = Assert.Single(entry.Daily);
        Assert.Equal(DateTime.UtcNow.Date, day.Date);
        Assert.Equal(2, day.Views);
    }

    [Fact]
    public async Task GetArticleViewHistory_IgnoresViewsOutsideRange()
    {
        var (_, activity, db) = CreateServices();
        var authorId = await CreateUserAsync(db);
        var articleId = await CreatePublishedArticleAsync(db, authorId, "Beitrag");

        await activity.LogArticleViewAsync(articleId, null, "127.0.0.1");

        // Zeitraum in der Zukunft — der heutige View liegt außerhalb.
        var from = DateTime.UtcNow.Date.AddDays(1);
        var to = DateTime.UtcNow.Date.AddDays(2);
        var history = await activity.GetArticleViewHistoryAsync(from, to);

        Assert.Empty(history);
    }
}
