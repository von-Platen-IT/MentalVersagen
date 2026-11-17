using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests for the admin-managed category catalogue: slug generation, the protected
/// "undefined" system category, rename stability and article reassignment on delete.
/// </summary>
public class CategoryServiceTests
{
    private static CategoryService CreateService(AppDbContext db) => new(db, new SlugGenerator());

    [Fact]
    public async Task GetUndefinedAsync_ReturnsProtectedSystemCategory()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var undefined = await service.GetUndefinedAsync();

        Assert.Equal(CategoryIds.Undefined, undefined.Id);
        Assert.Equal("undefined", undefined.Slug);
        Assert.True(undefined.IsSystem);
    }

    [Fact]
    public async Task DeleteAsync_Undefined_IsRejected()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.DeleteAsync(CategoryIds.Undefined);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.True(await db.Categories.AnyAsync(c => c.Id == CategoryIds.Undefined));
    }

    [Fact]
    public async Task UpdateAsync_Undefined_RenameIsRejected()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.UpdateAsync(
            CategoryIds.Undefined, new CategoryInput("Anders", null, null, null));

        Assert.False(result.Succeeded);
        Assert.Equal("undefined", (await db.Categories.FindAsync(CategoryIds.Undefined))!.Name);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueSlugWithSuffix()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.CreateAsync(
            new CategoryInput("Technik", "politik", null, null));

        Assert.True(result.Succeeded);
        Assert.Equal("politik-2", result.Category!.Slug);
    }

    [Fact]
    public async Task UpdateAsync_RenameKeepsStableSlug()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            new CategoryInput("Technik", null, null, null));
        Assert.True(created.Succeeded);

        var updated = await service.UpdateAsync(
            created.Category!.Id, new CategoryInput("Zukunft", null, null, null));

        Assert.True(updated.Succeeded);
        Assert.Equal("Zukunft", updated.Category!.Name);
        Assert.Equal("technik", updated.Category.Slug);
    }

    [Fact]
    public async Task DeleteAsync_ReassignsArticlesToUndefined()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            new CategoryInput("Technik", null, null, null));
        var article = new Article
        {
            Title = "Akte",
            ContentMarkdown = "Inhalt",
            CategoryId = created.Category!.Id,
            AuthorId = Guid.NewGuid()
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(created.Category.Id);

        Assert.True(result.Succeeded);

        var reloaded = await db.Articles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(CategoryIds.Undefined, reloaded.CategoryId);
        Assert.False(await db.Categories.AnyAsync(c => c.Id == created.Category.Id));
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsArticleCounts()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var article = new Article
        {
            Title = "Akte",
            ContentMarkdown = "Inhalt",
            CategoryId = CategoryIds.Politik,
            AuthorId = Guid.NewGuid()
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var usage = await service.GetUsageAsync();

        Assert.Equal(
            1,
            usage.Single(u => u.Category.Id == CategoryIds.Politik).ArticleCount);
        Assert.True(usage.Single(u => u.Category.Id == CategoryIds.Undefined).Category.IsSystem);
    }

    [Fact]
    public async Task AssignArticleAsync_ChangesArticleCategory()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            new CategoryInput("Technik", null, null, null));
        var article = new Article
        {
            Title = "Akte",
            ContentMarkdown = "Inhalt",
            CategoryId = CategoryIds.Undefined,
            AuthorId = Guid.NewGuid()
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var result = await service.AssignArticleAsync(article.Id, created.Category!.Id);

        Assert.True(result.Succeeded);
        var reloaded = await db.Articles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal(created.Category.Id, reloaded.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_StoresNormalizedColor()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.CreateAsync(
            new CategoryInput("Technik", null, null, null, 0, "#ABCDEF"));

        Assert.True(result.Succeeded);
        Assert.Equal("#abcdef", result.Category!.Color);
    }

    [Fact]
    public async Task CreateAsync_InvalidColor_FallsBackToDefault()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.CreateAsync(
            new CategoryInput("Technik", null, null, null, 0, "rot"));

        Assert.True(result.Succeeded);
        Assert.Equal("#7f8c8d", result.Category!.Color);
    }

    [Fact]
    public async Task UpdateAsync_SystemCategory_AllowsColorChange()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var result = await service.UpdateAsync(
            CategoryIds.Undefined,
            new CategoryInput("undefined", null, null, null, 0, "#123456"));

        Assert.True(result.Succeeded);
        Assert.Equal("#123456", result.Category!.Color);
    }
}