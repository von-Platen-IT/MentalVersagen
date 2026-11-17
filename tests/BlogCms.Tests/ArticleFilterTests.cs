using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der URL-basierten Mehrfachfilter: mehrere Kategorien (immer ODER),
/// mehrere Hashtags mit <c>any</c>/<c>all</c> und die Kombination beider Filter.
/// </summary>
public class ArticleFilterTests
{
    private static ArticleService CreateService(AppDbContext db) => new(db, new SlugGenerator());

    private static async Task<Guid> CreateUserAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"filter-{suffix}@example.com",
            Email = $"filter-{suffix}@example.com",
            DisplayName = "Filter-Testautor"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Article NewArticle(
        string title, Guid categoryId, DateTime publishedAt, Guid authorId) => new()
    {
        Title = title,
        Slug = title.ToLowerInvariant().Replace(' ', '-'),
        Excerpt = "Teaser",
        ContentMarkdown = "Inhalt",
        CategoryId = categoryId,
        AuthorId = authorId,
        AccessLevel = ArticleAccessLevel.Public,
        Status = ArticleStatus.Published,
        PublishedAt = publishedAt,
        UpdatedAt = publishedAt
    };

    [Fact]
    public async Task SearchPublished_MultipleCategories_ReturnsUnion()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(NewArticle("Politik A", CategoryIds.Politik, now, authorId), [], []);
        await service.CreateAsync(
            NewArticle("Satire B", CategoryIds.Satire, now.AddMinutes(-1), authorId), [], []);
        await service.CreateAsync(
            NewArticle("Verschwoerung C", CategoryIds.Verschwoerungstheorien, now.AddMinutes(-2), authorId),
            [], []);

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10, CategoryIds: [CategoryIds.Politik, CategoryIds.Satire]));

        Assert.Equal(2, total);
        Assert.All(items, a => Assert.Contains(
            a.CategoryId, new[] { CategoryIds.Politik, CategoryIds.Satire }));
    }

    [Fact]
    public async Task SearchPublished_MultipleHashtagsAny_ReturnsUnion()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(NewArticle("A", CategoryIds.Politik, now, authorId), [], ["DeepDive"]);
        await service.CreateAsync(
            NewArticle("B", CategoryIds.Politik, now.AddMinutes(-1), authorId), [], ["Faktencheck"]);
        await service.CreateAsync(
            NewArticle("C", CategoryIds.Politik, now.AddMinutes(-2), authorId), [], ["Sonstiges"]);

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10, HashtagSlugs: ["deepdive", "faktencheck"], Match: ArticleFilterMatch.Any));

        Assert.Equal(2, total);
        Assert.DoesNotContain(items, a => a.Title == "C");
    }

    [Fact]
    public async Task SearchPublished_MultipleHashtagsAll_RequiresEveryHashtag()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(
            NewArticle("A", CategoryIds.Politik, now, authorId), [], ["DeepDive", "Faktencheck"]);
        await service.CreateAsync(
            NewArticle("B", CategoryIds.Politik, now.AddMinutes(-1), authorId), [], ["DeepDive"]);

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10, HashtagSlugs: ["deepdive", "faktencheck"], Match: ArticleFilterMatch.All));

        Assert.Equal(1, total);
        Assert.Equal("A", items[0].Title);
    }

    [Fact]
    public async Task SearchPublished_CategoryAndHashtag_CombinesWithAnd()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(NewArticle("A", CategoryIds.Politik, now, authorId), [], ["DeepDive"]);
        await service.CreateAsync(
            NewArticle("B", CategoryIds.Satire, now.AddMinutes(-1), authorId), [], ["DeepDive"]);

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10, CategoryIds: [CategoryIds.Politik], HashtagSlugs: ["deepdive"]));

        Assert.Equal(1, total);
        Assert.Equal("A", items[0].Title);
    }

    [Fact]
    public async Task SearchPublished_UnknownHashtag_ReturnsEmpty()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(NewArticle("A", CategoryIds.Politik, now, authorId), [], ["DeepDive"]);

        var (_, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10, HashtagSlugs: ["gibtsnicht"]));

        Assert.Equal(0, total);
    }

    [Fact]
    public async Task SearchPublished_SingleAliasAndList_AreMerged()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var authorId = await CreateUserAsync(db);
        var now = DateTime.UtcNow;

        await service.CreateAsync(NewArticle("A", CategoryIds.Politik, now, authorId), [], ["DeepDive"]);
        await service.CreateAsync(
            NewArticle("B", CategoryIds.Satire, now.AddMinutes(-1), authorId), [], ["Faktencheck"]);

        var (_, total) = await service.SearchPublishedAsync(new ArticleQuery(
            1, 10,
            CategoryId: CategoryIds.Politik,
            CategoryIds: [CategoryIds.Satire]));

        Assert.Equal(2, total);
    }
}
