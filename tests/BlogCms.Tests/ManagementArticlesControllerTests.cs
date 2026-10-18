using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
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
/// Tests des Management-Artikel-Controllers: DTO-Mapping, Feldfehler-Antworten
/// (ProblemDetails mit errors[]), Statuscodes sowie Autorenzuordnung.
/// </summary>
public class ManagementArticlesControllerTests
{
    // --- Test-Infrastruktur ---

    private static async Task<Guid> CreateUserAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"api-autor-{suffix}@example.com",
            Email = $"api-autor-{suffix}@example.com",
            DisplayName = "API-Testautor"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static ManagementArticlesController BuildController(AppDbContext db, Guid? authorId)
    {
        var articles = new ArticleService(db, new SlugGenerator());
        var media = new MediaService(db, new NullImageProcessor(), new NullStorageService(),
            Options.Create(new MediaOptions()));
        var posting = new ArticlePostingService(
            db, articles, media, new StubOEmbedService(), new ActivityLogService(db));

        return new ManagementArticlesController(
            posting, articles,
            Options.Create(new ApiOptions { Key = "test-key", AuthorId = authorId }),
            NullLogger<ManagementArticlesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static async Task<(ManagementArticlesController Controller, AppDbContext Db, Guid AuthorId)> CreateControllerAsync()
    {
        var db = TestDb.Create();
        var authorId = await CreateUserAsync(db);
        return (BuildController(db, authorId), db, authorId);
    }

    private static (ManagementArticlesController Controller, AppDbContext Db) CreateControllerWithoutAuthor()
    {
        var db = TestDb.Create();
        return (BuildController(db, authorId: null), db);
    }

    private static ArticleWriteModel BuildModel(
        ArticleStatus status = ArticleStatus.Published,
        string? titleImageUrl = "https://example.com/cover.jpg",
        DateTime? scheduledAt = null) =>
        new()
        {
            Title = "API-Testbeitrag",
            Excerpt = "Kurzer Teaser",
            ContentMarkdown = "Inhalt in Markdown",
            Category = ArticleCategory.Politik,
            AccessLevel = ArticleAccessLevel.Public,
            Status = status,
            TitleImageUrl = titleImageUrl,
            ScheduledAt = scheduledAt
        };

    // --- createArticle ---

    [Fact]
    public async Task CreateArticle_ValidPublished_Returns201WithReadModel()
    {
        var (controller, _, authorId) = await CreateControllerAsync();

        var result = await controller.CreateArticle(BuildModel(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var readModel = Assert.IsType<ArticleReadModel>(created.Value);
        Assert.NotEqual(Guid.Empty, readModel.Id);
        Assert.False(string.IsNullOrWhiteSpace(readModel.Slug));
        Assert.Equal(ArticleStatus.Published, readModel.Status);
        Assert.Equal(authorId, readModel.AuthorId);
    }

    [Fact]
    public async Task CreateArticle_MissingTitleImageSource_Returns400WithFieldErrors()
    {
        var (controller, _, _) = await CreateControllerAsync();

        var result = await controller.CreateArticle(BuildModel(titleImageUrl: null), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);

        var errors = Assert.IsAssignableFrom<IReadOnlyList<ApiFieldError>>(problem.Extensions["errors"]);
        Assert.Contains(errors, e => e.Field == PostError.ImageUploadField);
    }

    [Fact]
    public async Task CreateArticle_PublishedWithoutExcerpt_Returns400FieldErrors()
    {
        var (controller, _, _) = await CreateControllerAsync();
        var model = BuildModel();
        model.Excerpt = null;

        var result = await controller.CreateArticle(model, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<ApiFieldError>>(problem.Extensions["errors"]);
        Assert.Contains(errors, e => e.Field == PostError.ExcerptField);
    }

    [Fact]
    public async Task CreateArticle_ScheduledInPast_Returns400AtScheduledAt()
    {
        var (controller, _, _) = await CreateControllerAsync();

        var result = await controller.CreateArticle(
            BuildModel(ArticleStatus.Scheduled, scheduledAt: DateTime.UtcNow.AddHours(-1)), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<ApiFieldError>>(problem.Extensions["errors"]);
        Assert.Contains(errors, e => e.Field == PostError.ScheduledAtField);
    }

    [Fact]
    public async Task CreateArticle_WithoutAuthorId_Returns503()
    {
        // Ohne Api:AuthorId kann kein Artikel angelegt werden — die API meldet 503.
        var (controller, _) = CreateControllerWithoutAuthor();

        var result = await controller.CreateArticle(BuildModel(), default);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    // --- getArticle / listArticles ---

    [Fact]
    public async Task GetArticle_Existing_Returns200WithReadModel()
    {
        var (controller, _, _) = await CreateControllerAsync();
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateArticle(BuildModel(), default));
        var createdModel = Assert.IsType<ArticleReadModel>(created.Value);

        var result = await controller.GetArticle(createdModel.Id, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var readModel = Assert.IsType<ArticleReadModel>(ok.Value);
        Assert.Equal(createdModel.Id, readModel.Id);
        Assert.Equal("API-Testbeitrag", readModel.Title);
    }

    [Fact]
    public async Task GetArticle_UnknownId_Returns404ProblemDetails()
    {
        var (controller, _) = CreateControllerWithoutAuthor();

        var result = await controller.GetArticle(Guid.NewGuid(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.IsType<ProblemDetails>(notFound.Value);
    }

    [Fact]
    public async Task ListArticles_ReturnsPagedResponse()
    {
        var (controller, _, _) = await CreateControllerAsync();
        await controller.CreateArticle(BuildModel(), default);
        await controller.CreateArticle(BuildModel(), default);

        var result = await controller.ListArticles(page: 1, pageSize: 10, cancellationToken: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsAssignableFrom<PagedResponse<ArticleReadModel>>(ok.Value);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
    }

    // --- updateArticle ---

    [Fact]
    public async Task UpdateArticle_UnknownId_Returns404()
    {
        var (controller, _) = CreateControllerWithoutAuthor();

        var result = await controller.UpdateArticle(Guid.NewGuid(), BuildModel(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.IsType<ProblemDetails>(notFound.Value);
    }

    [Fact]
    public async Task UpdateArticle_Valid_Returns200WithUpdatedModel()
    {
        var (controller, _, _) = await CreateControllerAsync();
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateArticle(BuildModel(), default));
        var createdModel = Assert.IsType<ArticleReadModel>(created.Value);

        var model = BuildModel(ArticleStatus.Draft, titleImageUrl: null);
        model.Title = "Geänderter Titel";

        var result = await controller.UpdateArticle(createdModel.Id, model, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var readModel = Assert.IsType<ArticleReadModel>(ok.Value);
        Assert.Equal("Geänderter Titel", readModel.Title);
        Assert.Equal(ArticleStatus.Draft, readModel.Status);
    }

    // --- deleteArticle ---

    [Fact]
    public async Task DeleteArticle_Existing_Returns204()
    {
        var (controller, db, _) = await CreateControllerAsync();
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateArticle(BuildModel(), default));
        var createdModel = Assert.IsType<ArticleReadModel>(created.Value);

        var result = await controller.DeleteArticle(createdModel.Id, default);

        Assert.IsType<NoContentResult>(result);

        // Soft-Delete: Der Artikel bleibt in der Datenbank erhalten.
        var reloaded = await db.Articles.FindAsync(createdModel.Id);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.DeletedAt);
    }

    [Fact]
    public async Task DeleteArticle_UnknownId_Returns404()
    {
        var (controller, _) = CreateControllerWithoutAuthor();

        var result = await controller.DeleteArticle(Guid.NewGuid(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.IsType<ProblemDetails>(notFound.Value);
    }

    // --- Test doubles (analog zu ArticlePostingServiceTests) ---

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
