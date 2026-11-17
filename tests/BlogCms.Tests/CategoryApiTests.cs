using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Web.Controllers;
using BlogCms.Web.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der Kategorien-Management-API: Auflisten, Anlegen, Bearbeiten, Löschen,
/// Schutz der Systemkategorie und Artikel-Umzug beim Löschen.
/// </summary>
public class CategoryApiTests
{
    private static ManagementCategoriesController CreateController(AppDbContext db) =>
        new(new CategoryService(db, new SlugGenerator()),
            NullLogger<ManagementCategoriesController>.Instance);

    [Fact]
    public async Task ListCategories_ReturnsSeededCategoriesWithCounts()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.ListCategories(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<CategoryReadModel>>(ok.Value);
        Assert.Contains(items, c => c.Slug == CategoryIds.UndefinedSlug && c.IsSystem);
        Assert.Contains(items, c => c.Slug == CategoryIds.PolitikSlug);
    }

    [Fact]
    public async Task CreateCategory_Valid_Returns201WithGeneratedSlug()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.CreateCategory(
            new CategoryWriteModel { Name = "Hintergründe", Description = "Tiefe Analysen" },
            default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var model = Assert.IsType<CategoryReadModel>(created.Value);
        Assert.Equal("hintergruende", model.Slug);
        Assert.False(model.IsSystem);
        Assert.Equal("Tiefe Analysen", model.Description);
    }

    [Fact]
    public async Task CreateCategory_WithColor_ReturnsNormalizedColor()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.CreateCategory(
            new CategoryWriteModel { Name = "Technik", Color = "#ABCDEF" }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var model = Assert.IsType<CategoryReadModel>(created.Value);
        Assert.Equal("#abcdef", model.Color);
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_Returns400()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.CreateCategory(
            new CategoryWriteModel { Name = "Politik" }, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCategory_UndefinedRename_Returns409()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.UpdateCategory(
            CategoryIds.UndefinedSlug, new CategoryWriteModel { Name = "Anders" }, default);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task DeleteCategory_Undefined_Returns409()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.DeleteCategory(CategoryIds.UndefinedSlug, default);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task DeleteCategory_ReassignsArticlesToUndefined()
    {
        await using var db = TestDb.Create();
        var articles = new ArticleService(db, new SlugGenerator());
        var now = DateTime.UtcNow;
        var article = new Article
        {
            Title = "A",
            Slug = "a",
            Excerpt = "e",
            ContentMarkdown = "c",
            CategoryId = CategoryIds.Politik,
            Status = ArticleStatus.Published,
            PublishedAt = now,
            UpdatedAt = now
        };
        await articles.CreateAsync(article, [], []);

        var controller = CreateController(db);
        var result = await controller.DeleteCategory(CategoryIds.PolitikSlug, default);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await db.Articles.FindAsync(article.Id);
        Assert.Equal(CategoryIds.Undefined, reloaded!.CategoryId);
    }

    [Fact]
    public async Task GetCategory_Unknown_Returns404()
    {
        await using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.GetCategory("gibtsnicht", default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetCategory_BySlug_ReturnsArticleCount()
    {
        await using var db = TestDb.Create();
        var articles = new ArticleService(db, new SlugGenerator());
        var now = DateTime.UtcNow;
        await articles.CreateAsync(new Article
        {
            Title = "A",
            Slug = "a",
            Excerpt = "e",
            ContentMarkdown = "c",
            CategoryId = CategoryIds.Politik,
            Status = ArticleStatus.Published,
            PublishedAt = now,
            UpdatedAt = now
        }, [], []);

        var controller = CreateController(db);
        var result = await controller.GetCategory(CategoryIds.PolitikSlug, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<CategoryReadModel>(ok.Value);
        Assert.Equal(1, model.ArticleCount);
    }
}
