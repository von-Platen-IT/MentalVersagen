using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Web.Configuration;
using BlogCms.Web.Controllers;
using BlogCms.Web.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der Hashtag-API: gezieltes Ersetzen/Entfernen der Hashtags eines
/// Artikels sowie die Hashtag-Liste mit Nutzungszahlen.
/// </summary>
public class ArticleHashtagApiTests
{
    private static ManagementArticlesController CreateController(AppDbContext db)
    {
        var articles = new ArticleService(db, new SlugGenerator());
        var categories = new CategoryService(db, new SlugGenerator());

        return new ManagementArticlesController(
            null!, articles, categories,
            Options.Create(new ApiOptions { Key = "test-key" }),
            NullLogger<ManagementArticlesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static async Task<Article> CreateArticleAsync(
        AppDbContext db, params string[] hashtags)
    {
        var service = new ArticleService(db, new SlugGenerator());
        var now = DateTime.UtcNow;
        var article = new Article
        {
            Title = "Hashtag-Test",
            Slug = "hashtag-test",
            Excerpt = "Teaser",
            ContentMarkdown = "Inhalt",
            CategoryId = CategoryIds.Politik,
            Status = ArticleStatus.Published,
            PublishedAt = now,
            UpdatedAt = now
        };
        return await service.CreateAsync(article, [], hashtags);
    }

    [Fact]
    public async Task SetHashtagsAsync_ReplacesExistingHashtags()
    {
        await using var db = TestDb.Create();
        var article = await CreateArticleAsync(db, "Alt");
        var service = new ArticleService(db, new SlugGenerator());

        var updated = await service.SetHashtagsAsync(article.Id, ["Neu", "Zweiter"]);

        Assert.NotNull(updated);
        var names = updated!.ArticleHashtags.Select(ah => ah.Hashtag!.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Neu", "Zweiter"], names);
    }

    [Fact]
    public async Task SetHashtagsAsync_EmptyList_ClearsHashtags()
    {
        await using var db = TestDb.Create();
        var article = await CreateArticleAsync(db, "Alt");
        var service = new ArticleService(db, new SlugGenerator());

        var updated = await service.SetHashtagsAsync(article.Id, []);

        Assert.NotNull(updated);
        Assert.Empty(updated!.ArticleHashtags);
    }

    [Fact]
    public async Task SetHashtagsAsync_UnknownArticle_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var service = new ArticleService(db, new SlugGenerator());

        var updated = await service.SetHashtagsAsync(Guid.NewGuid(), ["Neu"]);

        Assert.Null(updated);
    }

    [Fact]
    public async Task SetArticleHashtags_Returns200WithNewHashtags()
    {
        await using var db = TestDb.Create();
        var article = await CreateArticleAsync(db, "Alt");
        var controller = CreateController(db);

        var result = await controller.SetArticleHashtags(
            article.Id, new HashtagUpdateModel { Hashtags = ["DeepDive", "Faktencheck"] }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<ArticleReadModel>(ok.Value);
        Assert.Equal(2, model.Hashtags.Count);
        Assert.Contains("DeepDive", model.Hashtags);
    }

    [Fact]
    public async Task ClearArticleHashtags_Returns204()
    {
        await using var db = TestDb.Create();
        var article = await CreateArticleAsync(db, "Alt");
        var controller = CreateController(db);

        var result = await controller.ClearArticleHashtags(article.Id, default);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await db.Articles.FindAsync(article.Id);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public async Task SetArticleHashtags_UnknownArticle_Returns404()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.SetArticleHashtags(
            Guid.NewGuid(), new HashtagUpdateModel { Hashtags = ["Neu"] }, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ListHashtags_ReturnsUsageCounts()
    {
        await using var db = TestDb.Create();
        await CreateArticleAsync(db, "DeepDive");
        var articles = new ArticleService(db, new SlugGenerator());
        var controller = new ManagementHashtagsController(articles);

        var result = await controller.ListHashtags(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<HashtagUsage>>(ok.Value);
        Assert.Contains(items, h => h.Slug == "deepdive" && h.Count == 1);
    }
}
