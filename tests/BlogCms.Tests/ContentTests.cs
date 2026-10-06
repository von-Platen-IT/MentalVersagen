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

    [Fact]
    public async Task SearchPublishedAsync_ReturnsLatestPublishedArticlesFirst_WithDeterministicOrdering()
    {
        var service = CreateService(out var db);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "autor@example.com",
            Email = "autor@example.com",
            DisplayName = "Testautor"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var authorId = user.Id;

        var older = await service.CreateAsync(new Article
        {
            Title = "Older Article",
            ContentMarkdown = "x",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            AuthorId = authorId,
            PublishedAt = DateTime.UtcNow.AddHours(-2)
        }, []);

        var newer1 = await service.CreateAsync(new Article
        {
            Title = "Newer Article 1",
            ContentMarkdown = "x",
            Category = ArticleCategory.Satire,
            Status = ArticleStatus.Published,
            AuthorId = authorId,
            PublishedAt = DateTime.UtcNow
        }, []);

        var newer2 = await service.CreateAsync(new Article
        {
            Title = "Newer Article 2",
            ContentMarkdown = "x",
            Category = ArticleCategory.Verschwoerungstheorien,
            Status = ArticleStatus.Published,
            AuthorId = authorId,
            PublishedAt = DateTime.UtcNow
        }, []);

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(1, 10));

        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
        // Newer articles must precede the older article
        Assert.Equal("Older Article", items[2].Title);
        Assert.Contains(items[0].Title, new[] { "Newer Article 1", "Newer Article 2" });
    }

    [Theory]
    [InlineData(ArticleStatus.Draft, "Entwurf")]
    [InlineData(ArticleStatus.Published, "Veröffentlicht")]
    [InlineData(ArticleStatus.Scheduled, "Geplant")]
    [InlineData(ArticleStatus.Archived, "Archiviert")]
    public void StatusLabel_ReturnsCorrectLocalizedName(ArticleStatus status, string expected)
    {
        Assert.Equal(expected, BlogCms.Web.Content.ArticleDisplay.StatusLabel(status));
    }

    [Fact]
    public void ArticlePostRequest_HasTitleImageSource_RecognizesUploadedEditorImages()
    {
        var request = new BlogCms.Infrastructure.Content.ArticlePostRequest(
            "Titel", null, null, "Teaser", "Inhalt",
            ArticleCategory.Politik, ArticleAccessLevel.Public, ArticleStatus.Draft,
            null, [], [], null, null, [], [Guid.NewGuid()]);

        Assert.True(request.HasTitleImageSource(hasExistingImage: false));
    }

    [Fact]
    public async Task CreateAsync_WithUploadedMedia_PublishesToSearch_AndResolvesCoverImage()
    {
        var service = CreateService(out var db);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "uploader@example.com",
            Email = "uploader@example.com",
            DisplayName = "Uploader"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var article = new Article
        {
            Title = "Neuer Artikel mit Bild",
            ContentMarkdown = "Spannender Inhalt",
            Excerpt = "Kurzer Teaser",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            AuthorId = user.Id
        };

        var created = await service.CreateAsync(article, ["politik"]);

        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            ArticleId = created.Id,
            UploadedByUserId = user.Id,
            StoragePath = "uploads/cover.jpg",
            MimeType = "image/jpeg",
            IsCover = true
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var (items, total) = await service.SearchPublishedAsync(new ArticleQuery(1, 10));

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("Neuer Artikel mit Bild", items[0].Title);
        Assert.Single(items[0].MediaAssets);
        Assert.True(items[0].MediaAssets.First().IsCover);
    }
}

public class VideoEmbedRendererTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?list=PL123&v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    public void ResolveEmbedUrl_ExtractsYouTubeId(string url, string expected)
    {
        var result = BlogCms.Web.Content.VideoEmbedRenderer.ResolveEmbedUrl(VideoPlatform.YouTube, url);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://vimeo.com/123456789", "https://player.vimeo.com/video/123456789")]
    [InlineData("https://player.vimeo.com/video/123456789", "https://player.vimeo.com/video/123456789")]
    public void ResolveEmbedUrl_ExtractsVimeoId(string url, string expected)
    {
        var result = BlogCms.Web.Content.VideoEmbedRenderer.ResolveEmbedUrl(VideoPlatform.Vimeo, url);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveEmbedUrl_ReturnsNull_ForUnsupportedPlatform()
    {
        var result = BlogCms.Web.Content.VideoEmbedRenderer.ResolveEmbedUrl(
            VideoPlatform.X, "https://x.com/user/status/123");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEmbedUrl_ReturnsNull_ForYouTubeUrlWithoutId()
    {
        var result = BlogCms.Web.Content.VideoEmbedRenderer.ResolveEmbedUrl(
            VideoPlatform.YouTube, "https://www.youtube.com/");

        Assert.Null(result);
    }

    [Fact]
    public void Render_ProducesDirectIframe_WithAllowAttributes()
    {
        var embed = new VideoEmbed
        {
            Platform = VideoPlatform.YouTube,
            OriginalUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            EmbedHtml = "<iframe src=\"https://www.youtube.com/embed/dQw4w9WgXcQ\"></iframe>"
        };

        var html = BlogCms.Web.Content.VideoEmbedRenderer.Render(embed);

        Assert.Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", html);
        Assert.Contains("allowfullscreen", html);
        Assert.Contains("allow=\"accelerometer", html);
        Assert.DoesNotContain("srcdoc", html);
    }

    [Fact]
    public void Render_FallsBackToLink_ForUnsupportedPlatform()
    {
        var embed = new VideoEmbed
        {
            Platform = VideoPlatform.Other,
            OriginalUrl = "https://example.com/video"
        };

        var html = BlogCms.Web.Content.VideoEmbedRenderer.Render(embed);

        Assert.Contains("<a href=", html);
        Assert.DoesNotContain("<iframe", html);
    }
}
