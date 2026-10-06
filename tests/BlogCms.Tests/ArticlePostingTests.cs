using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests for the article posting workflow (IArticlePostingService): validation,
/// target status resolution, persistence incl. tags/hashtags/activity log and
/// field-level error reporting for rejected uploads.
/// </summary>
public class ArticlePostingServiceTests
{
    private static (ArticlePostingService Service, AppDbContext Db) CreateService()
    {
        var db = TestDb.Create();
        var articles = new ArticleService(db, new SlugGenerator());
        var media = new MediaService(db, new NullImageProcessor(), new NullStorageService(),
            Microsoft.Extensions.Options.Options.Create(new MediaOptions()));
        var oEmbed = new StubOEmbedService();
        var activity = new ActivityLogService(db);
        return (new ArticlePostingService(db, articles, media, oEmbed, activity), db);
    }

    /// <summary>
    /// Creates a real user row: AuthorId is a foreign key to User (DataSchema §1/§2),
    /// and the InMemory provider drops rows whose required Include principal is missing.
    /// </summary>
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

    private static ArticlePostRequest BuildRequest(
        string title = "Testbeitrag",
        string? slug = null,
        string? titleImageUrl = "https://example.com/cover.jpg",
        ArticleStatus status = ArticleStatus.Published,
        DateTime? scheduledAt = null,
        string? videoUrl = null,
        ArticleUpload? coverUpload = null,
        string? tags = null) =>
        new(
            title, slug, titleImageUrl, "Kurzer Teaser", "Inhalt in Markdown",
            ArticleCategory.Politik, ArticleAccessLevel.Public, status, scheduledAt,
            ArticlePostRequest.ParseTags(tags), ArticlePostRequest.ParseHashtags(null),
            videoUrl, coverUpload, [], []);

    // --- Validation (BR-022 / BR-032) ---

    [Fact]
    public void Validate_MissingTitleImageSource_ReturnsErrorAtImageUpload()
    {
        var (service, _) = CreateService();
        var request = BuildRequest(titleImageUrl: null);

        var errors = service.Validate(request, hasExistingImage: false);

        Assert.Single(errors);
        Assert.Equal(PostError.ImageUploadField, errors[0].Field);
    }

    [Fact]
    public void Validate_ExistingImageOnEdit_SatisfiesTitleImageRule()
    {
        var (service, _) = CreateService();
        var request = BuildRequest(titleImageUrl: null);

        Assert.Empty(service.Validate(request, hasExistingImage: true));
    }

    [Fact]
    public void Validate_ScheduledWithoutFutureDate_ReturnsErrorAtScheduledAt()
    {
        var (service, _) = CreateService();
        var request = BuildRequest(status: ArticleStatus.Scheduled, scheduledAt: DateTime.UtcNow.AddHours(-1));

        var errors = service.Validate(request, hasExistingImage: false);

        Assert.Contains(errors, e => e.Field == PostError.ScheduledAtField);
    }

    // --- Target status resolution (publish action) ---

    [Theory]
    [InlineData(ArticleStatus.Scheduled, true, ArticleStatus.Scheduled)]
    [InlineData(ArticleStatus.Scheduled, false, ArticleStatus.Published)]
    [InlineData(ArticleStatus.Draft, true, ArticleStatus.Draft)]
    [InlineData(ArticleStatus.Published, false, ArticleStatus.Published)]
    public void ResolveTargetStatus_AppliesPublishRule(
        ArticleStatus selected, bool futureDate, ArticleStatus expected)
    {
        var scheduledAt = futureDate ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow.AddHours(-1);

        Assert.Equal(expected, ArticleValidator.ResolveTargetStatus(selected, scheduledAt));
    }

    // --- Create workflow ---

    [Fact]
    public async Task CreateAsync_PersistsArticle_TagsAndActivityLog()
    {
        var (service, db) = CreateService();
        var authorId = await CreateUserAsync(db);

        var result = await service.CreateAsync(BuildRequest(title: "Neue Akte", tags: "politik"), authorId);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Article);
        Assert.Equal("neue-akte", result.Article!.Slug);
        Assert.NotNull(result.Article.PublishedAt);

        var tag = await db.Tags.SingleAsync(t => t.Slug == "politik");
        Assert.Contains(await db.ArticleTags.ToListAsync(),
            at => at.ArticleId == result.Article.Id && at.TagId == tag.Id);

        // Activity log is written as part of the workflow (DataSchema §13).
        Assert.Contains(await db.ActivityLogEntries.ToListAsync(),
            e => e.EventType == ActivityEventType.ArticleCreated && e.ArticleId == result.Article.Id);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_PersistsNothing()
    {
        var (service, db) = CreateService();

        var result = await service.CreateAsync(BuildRequest(titleImageUrl: null), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Articles.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectedUpload_ReturnsFieldError()
    {
        var (service, db) = CreateService();
        await CreateUserAsync(db);

        // Empty stream -> MediaService rejects the upload ("Die Datei ist leer.").
        var upload = new ArticleUpload(new MemoryStream(), "cover.jpg");
        var result = await service.CreateAsync(
            BuildRequest(titleImageUrl: null, coverUpload: upload), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == PostError.ImageUploadField);
    }

    // --- Update workflow / slug stability ---

    [Fact]
    public async Task UpdateAsync_KeepsSlug_WhenTitleChangesWithoutSlugInput()
    {
        var (service, db) = CreateService();
        var authorId = await CreateUserAsync(db);
        var created = await service.CreateAsync(BuildRequest(title: "Erster Titel"), authorId);

        var updated = await service.UpdateAsync(
            created.Article!.Id,
            BuildRequest(title: "Völlig neuer Titel", slug: null),
            authorId);

        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors.Select(e => e.Message)));
        var reloaded = await db.Articles.SingleAsync(a => a.Id == created.Article.Id);
        Assert.Equal("erster-titel", reloaded.Slug);
        Assert.Equal("Völlig neuer Titel", reloaded.Title);
    }

    [Fact]
    public async Task UpdateAsync_RegeneratesSlug_WhenSlugExplicitlyChanged()
    {
        var (service, db) = CreateService();
        var authorId = await CreateUserAsync(db);
        var created = await service.CreateAsync(BuildRequest(title: "Erster Titel"), authorId);

        var updated = await service.UpdateAsync(
            created.Article!.Id,
            BuildRequest(title: "Erster Titel", slug: "brandneuer-slug"),
            authorId);

        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors.Select(e => e.Message)));
        var reloaded = await db.Articles.SingleAsync(a => a.Id == created.Article.Id);
        Assert.Equal("brandneuer-slug", reloaded.Slug);
    }

    [Fact]
    public async Task UpdateAsync_MissingArticle_Fails()
    {
        var (service, _) = CreateService();

        var result = await service.UpdateAsync(Guid.NewGuid(), BuildRequest(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == PostError.GenericField);
    }

    // --- Test doubles ---

    private sealed class NullImageProcessor : IImageProcessor
    {
        public Task<ProcessedImage?> ProcessAsync(Stream input, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessedImage?>(null);
    }

    private sealed class NullStorageService : IStorageService
    {
        public Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(key);

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string GetPublicUrl(string key) => $"/uploads/{key}";
    }

    private sealed class StubOEmbedService : IOEmbedService
    {
        public VideoPlatform DetectPlatform(string url) => VideoPlatform.Other;

        public Task<OEmbedResult> ResolveAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(new OEmbedResult(VideoPlatform.Other, false, null, null));
    }
}