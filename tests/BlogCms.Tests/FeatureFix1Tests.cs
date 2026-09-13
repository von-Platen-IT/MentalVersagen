using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.LinkLists;
using BlogCms.Infrastructure.Ratings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class RatingServiceTests
{
    [Fact]
    public async Task RateArticle_UpsertsSingleRating_AndSummarizes()
    {
        // Acceptance criterion: a user holds at most one rating per target; it can change.
        var db = TestDb.Create();
        var article = new Article { Title = "A", ContentMarkdown = "x", Category = ArticleCategory.Politik, Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, AuthorId = Guid.NewGuid() };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var service = new RatingService(db);
        var userId = Guid.NewGuid();

        await service.RateArticleAsync(article.Id, userId, RatingValue.ThumbUp);
        await service.RateArticleAsync(article.Id, userId, RatingValue.ThumbDown);

        var summary = await service.GetArticleSummaryAsync(article.Id);
        Assert.Equal(0, summary.ThumbUp);
        Assert.Equal(1, summary.ThumbDown);
        Assert.Equal(-1, summary.Score);
        Assert.Equal(1, await db.Ratings.CountAsync());
    }

    [Fact]
    public async Task RateArticle_RejectsAnonymous()
    {
        // Acceptance criterion: anonymous visitors must not rate.
        var db = TestDb.Create();
        var article = new Article { Title = "A", ContentMarkdown = "x", Category = ArticleCategory.Politik, Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, AuthorId = Guid.NewGuid() };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var service = new RatingService(db);
        var result = await service.RateArticleAsync(article.Id, Guid.Empty, RatingValue.ThumbUp);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RateComment_StoresAndReads()
    {
        var db = TestDb.Create();
        var comment = new Comment { ArticleId = Guid.NewGuid(), UserId = Guid.NewGuid(), ContentText = "hi" };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var service = new RatingService(db);
        var userId = Guid.NewGuid();

        await service.RateCommentAsync(comment.Id, userId, RatingValue.ThumbUp);

        var summaries = await service.GetCommentSummariesAsync([comment.Id]);
        Assert.Equal(1, summaries[comment.Id].ThumbUp);
        Assert.Equal(RatingValue.ThumbUp, (await service.GetUserCommentRatingsAsync([comment.Id], userId))[comment.Id]);
    }
}

public class ArticleFeatureTests
{
    private static ArticleService Create(AppDbContext db) => new(db, new SlugGenerator());

    /// <summary>Articles have a required author navigation, so tests must persist a real user.</summary>
    private static async Task<Guid> AddUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"{Guid.NewGuid():N}@example.com",
            Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = "Autor"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Article NewArticle(string title, Guid authorId, ArticleStatus status = ArticleStatus.Published, ArticleAccessLevel access = ArticleAccessLevel.Public) => new()
    {
        Title = title,
        ContentMarkdown = "Inhalt",
        Excerpt = "Kurz",
        Category = ArticleCategory.Politik,
        Status = status,
        AccessLevel = access,
        PublishedAt = status == ArticleStatus.Published ? DateTime.UtcNow : null,
        AuthorId = authorId
    };

    [Fact]
    public async Task SearchPublished_FiltersByHashtag_AndSearchTerm()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var authorId = await AddUserAsync(db);

        await service.CreateAsync(NewArticle("Linux auf alten PCs", authorId), [], ["linux", "retro"]);
        await service.CreateAsync(NewArticle("Politik heute", authorId), [], ["wahlen"]);

        var byHashtag = await service.SearchPublishedAsync(new ArticleQuery(1, 10, HashtagSlug: "linux"));
        Assert.Single(byHashtag.Items);
        Assert.Equal("Linux auf alten PCs", byHashtag.Items[0].Title);

        var bySearch = await service.SearchPublishedAsync(new ArticleQuery(1, 10, Search: "heute"));
        Assert.Single(bySearch.Items);
    }

    [Fact]
    public async Task SearchPublished_FiltersByAccessLevel()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var authorId = await AddUserAsync(db);

        await service.CreateAsync(NewArticle("Frei", authorId), []);
        await service.CreateAsync(NewArticle("Exklusiv", authorId, access: ArticleAccessLevel.Premium), []);

        var premium = await service.SearchPublishedAsync(
            new ArticleQuery(1, 10, AccessLevel: ArticleAccessLevel.Premium));

        Assert.Single(premium.Items);
        Assert.Equal("Exklusiv", premium.Items[0].Title);
        Assert.True(premium.Items[0].IsPremium);
    }

    [Fact]
    public async Task PublishDueScheduled_PublishesReachedArticles()
    {
        // Acceptance criterion: scheduled articles are published automatically when due.
        var db = TestDb.Create();
        var service = Create(db);
        var authorId = await AddUserAsync(db);

        var scheduled = await service.CreateAsync(new Article
        {
            Title = "Geplant",
            ContentMarkdown = "x",
            Excerpt = "Kurz",
            Category = ArticleCategory.Satire,
            Status = ArticleStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            AuthorId = authorId
        }, []);

        var notYet = await service.CreateAsync(new Article
        {
            Title = "Später",
            ContentMarkdown = "x",
            Excerpt = "Kurz",
            Category = ArticleCategory.Satire,
            Status = ArticleStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            AuthorId = authorId
        }, []);

        var published = await service.PublishDueScheduledAsync();

        Assert.Equal(1, published);
        Assert.Equal(ArticleStatus.Published, scheduled.Status);
        Assert.NotNull(scheduled.PublishedAt);
        Assert.Equal(ArticleStatus.Scheduled, notYet.Status);
    }

    [Fact]
    public async Task GetForAuthor_ReturnsOnlyOwnArticles()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var authorId = await AddUserAsync(db);
        var otherAuthorId = await AddUserAsync(db);

        await service.CreateAsync(NewArticle("Meins", authorId), []);

        await service.CreateAsync(NewArticle("Fremd", otherAuthorId), []);

        var result = await service.GetForAuthorAsync(authorId);

        Assert.Single(result);
        Assert.Equal("Meins", result[0].Title);
    }
}

public class LinkListServiceTests
{
    [Fact]
    public async Task CreateAndOrder_PreservesEditorialSequence()
    {
        var db = TestDb.Create();
        var service = new LinkListService(db, new SlugGenerator());

        var a = new Article { Title = "A", ContentMarkdown = "x", Category = ArticleCategory.Politik, Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, AuthorId = Guid.NewGuid() };
        var b = new Article { Title = "B", ContentMarkdown = "x", Category = ArticleCategory.Politik, Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, AuthorId = Guid.NewGuid() };
        db.Articles.AddRange(a, b);
        await db.SaveChangesAsync();

        var created = await service.CreateAsync("Lesetipp", null, "Beschreibung");
        Assert.True(created.Succeeded);

        // Deliberately order B before A (independent of the publishing date).
        await service.SetItemsAsync(created.LinkList!.Id, [b.Id, a.Id]);

        var loaded = await service.GetBySlugAsync(created.LinkList.Slug);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Items.Count);
        Assert.Equal(b.Id, loaded.Items.OrderBy(i => i.Position).First().ArticleId);
    }

    [Fact]
    public async Task Create_GeneratesUniqueSlug()
    {
        var db = TestDb.Create();
        var service = new LinkListService(db, new SlugGenerator());

        var first = await service.CreateAsync("Meine Liste", null, null);
        var second = await service.CreateAsync("Meine Liste", null, null);

        Assert.Equal("meine-liste", first.LinkList!.Slug);
        Assert.Equal("meine-liste-2", second.LinkList!.Slug);
    }
}

public class CommentHighlightTests
{
    private static CommentService Create(AppDbContext db) => new(db, Options.Create(new CommentOptions()));

    [Fact]
    public async Task SetHighlight_AllowsArticleAuthor_RejectsOthers()
    {
        // Acceptance criterion: admin, or the author of the article, may highlight.
        var db = TestDb.Create();
        var articleAuthor = Guid.NewGuid();
        var article = new Article
        {
            Title = "A", ContentMarkdown = "x", Excerpt = "k", Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, AuthorId = articleAuthor
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var service = Create(db);
        var comment = await service.AddAsync(article.Id, Guid.NewGuid(), "Kommentar", null);

        var asAuthor = await service.SetHighlightAsync(comment.Comment!.Id, true, articleAuthor, isAdmin: false);
        Assert.True(asAuthor.Succeeded);

        var asStranger = await service.SetHighlightAsync(comment.Comment.Id, false, Guid.NewGuid(), isAdmin: false);
        Assert.False(asStranger.Succeeded);

        var asAdmin = await service.SetHighlightAsync(comment.Comment.Id, false, Guid.NewGuid(), isAdmin: true);
        Assert.True(asAdmin.Succeeded);
    }
}
