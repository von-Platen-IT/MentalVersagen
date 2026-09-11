using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogCms.Tests;

public class SlugGeneratorTests
{
    private readonly SlugGenerator _generator = new();

    [Theory]
    [InlineData("Ärger mit Öl & Ü30", "aerger-mit-oel-ue30")]
    [InlineData("Hallo Welt!", "hallo-welt")]
    [InlineData("  Mehrfach   Space  ", "mehrfach-space")]
    public void Generate_ProducesReadableSlug(string input, string expected)
    {
        Assert.Equal(expected, _generator.Generate(input));
    }

    [Fact]
    public void Generate_ReturnsEmpty_ForNullOrWhitespace()
    {
        Assert.Equal(string.Empty, _generator.Generate("   "));
    }
}

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void ToSafeHtml_RendersMarkdown()
    {
        var html = _renderer.ToSafeHtml("# Titel\n\n**fett**");

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>fett</strong>", html);
    }

    [Fact]
    public void ToSafeHtml_StripsScriptInjection()
    {
        // Acceptance criterion: no raw HTML / script injection survives rendering.
        var html = _renderer.ToSafeHtml("Hallo <script>alert('xss')</script>");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToSafeHtml_StripsJavascriptLinks()
    {
        var html = _renderer.ToSafeHtml("[klick](javascript:alert(1))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }
}

public class ArticleServiceTests
{
    private static ArticleService CreateService(out AppDbContext db)
    {
        db = TestDb.Create();
        return new ArticleService(db, new SlugGenerator());
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueSlug_AndSetsPublishedAt()
    {
        var service = CreateService(out var db);

        var first = await service.CreateAsync(new Article
        {
            Title = "Doppelter Titel",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            AuthorId = Guid.NewGuid()
        }, ["Politik"]);

        var second = await service.CreateAsync(new Article
        {
            Title = "Doppelter Titel",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            AuthorId = Guid.NewGuid()
        }, []);

        Assert.Equal("doppelter-titel", first.Slug);
        Assert.Equal("doppelter-titel-2", second.Slug);
        Assert.NotNull(first.PublishedAt);
        Assert.Single(await db.Tags.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_DoesNotOverwritePublishedAt()
    {
        var service = CreateService(out _);

        var article = await service.CreateAsync(new Article
        {
            Title = "Einmal veröffentlicht",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Satire,
            Status = ArticleStatus.Published,
            AuthorId = Guid.NewGuid()
        }, []);

        var originalPublishedAt = article.PublishedAt;

        article.Status = ArticleStatus.Archived;
        await service.UpdateAsync(article, []);

        Assert.Equal(originalPublishedAt, article.PublishedAt);
    }

    [Fact]
    public async Task GetPublishedAsync_HidesDraftsAndDeleted()
    {
        var service = CreateService(out var db);

        await service.CreateAsync(new Article
        {
            Title = "Draft",
            ContentMarkdown = "x",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Draft,
            AuthorId = Guid.NewGuid()
        }, []);

        var published = await service.CreateAsync(new Article
        {
            Title = "Live",
            ContentMarkdown = "x",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            AuthorId = Guid.NewGuid()
        }, []);

        await service.SoftDeleteAsync(published.Id);

        var (items, total) = await service.GetPublishedAsync(1, 10);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }
}
