using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using BlogCms.Web.Extensions;
using BlogCms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

[Authorize(Policy = Policies.RequireAuthor)]
public class CreateModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly IOEmbedService _oEmbed;
    private readonly IActivityLogService _activity;
    private readonly UserManager<User> _userManager;

    public CreateModel(
        IArticleService articles,
        IMediaService media,
        IOEmbedService oEmbed,
        IActivityLogService activity,
        UserManager<User> userManager)
    {
        _articles = articles;
        _media = media;
        _oEmbed = oEmbed;
        _activity = activity;
        _userManager = userManager;
    }

    [BindProperty]
    public ArticleInputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        return await ProcessCreateAsync(Input.Status);
    }

    public async Task<IActionResult> OnPostPublishAsync()
    {
        // Wenn keine geplante Veröffentlichung mit Zukunftsdatum vorliegt, direkt veröffentlichen.
        var targetStatus = Input.Status == ArticleStatus.Scheduled && Input.ScheduledAt > DateTime.UtcNow
            ? ArticleStatus.Scheduled
            : ArticleStatus.Published;

        return await ProcessCreateAsync(targetStatus);
    }

    public async Task<IActionResult> OnPostSaveDraftAsync()
    {
        return await ProcessCreateAsync(ArticleStatus.Draft);
    }

    private async Task<IActionResult> ProcessCreateAsync(ArticleStatus targetStatus)
    {
        Input.Status = targetStatus;
        ValidateInput(hasExistingImage: false);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var authorIdValue = _userManager.GetUserId(User);
        if (authorIdValue is null || !Guid.TryParse(authorIdValue, out var authorId))
        {
            return Challenge();
        }

        var article = new Article
        {
            Title = Input.Title,
            Slug = Input.Slug ?? string.Empty,
            TitleImageUrl = Input.TitleImageUrl,
            ContentMarkdown = Input.ContentMarkdown,
            Excerpt = Input.Excerpt,
            Category = Input.Category,
            AccessLevel = Input.AccessLevel,
            Status = Input.Status,
            ScheduledAt = Input.Status == ArticleStatus.Scheduled ? Input.ScheduledAt : null,
            AuthorId = authorId
        };

        var created = await _articles.CreateAsync(
            article, ArticleInputModel.ParseTags(Input.Tags), ArticleInputModel.ParseHashtags(Input.Hashtags));

        // Unified activity log: record the creation (IP + author account).
        await _activity.LogArticleCreatedAsync(created.Id, authorId, HttpContext.GetClientIp());

        await ApplyMediaAsync(created.Id, authorId);

        // Attach images uploaded through the Markdown editor before the article existed.
        await _media.AttachToArticleAsync(Input.ParseUploadedImageIds(), created.Id, authorId);

        TempData["Message"] = article.Status switch
        {
            ArticleStatus.Published => $"Akte „{article.Title}“ wurde erfolgreich veröffentlicht und ist nun auf der Startseite sichtbar.",
            ArticleStatus.Scheduled => $"Akte „{article.Title}“ wurde für die geplante Veröffentlichung am {article.ScheduledAt:dd.MM.yyyy HH:mm} gespeichert.",
            _ => $"Akte „{article.Title}“ wurde als Entwurf gespeichert."
        };

        return RedirectToPage("Index");
    }

    /// <summary>Applies FeatureFix1 requirements: teaser always, title image and schedule time.</summary>
    private void ValidateInput(bool hasExistingImage)
    {
        if (!Input.HasTitleImageSource(hasExistingImage))
        {
            ModelState.AddModelError(
                nameof(Input.ImageUpload),
                "Bitte eine Titelbild-URL angeben oder ein Bild hochladen (FeatureFix1 BR-022).");
        }

        if (Input.Status == ArticleStatus.Scheduled &&
            (Input.ScheduledAt is null || Input.ScheduledAt <= DateTime.UtcNow))
        {
            ModelState.AddModelError(
                nameof(Input.ScheduledAt),
                "Für eine geplante Veröffentlichung ist ein zukünftiger Zeitpunkt erforderlich.");
        }
    }

    private async Task ApplyMediaAsync(Guid articleId, Guid authorId)
    {
        if (!string.IsNullOrWhiteSpace(Input.VideoUrl))
        {
            var resolved = await _oEmbed.ResolveAsync(Input.VideoUrl);
            await _articles.SetVideoEmbedAsync(articleId, Input.VideoUrl, resolved);
        }

        if (Input.ImageUpload is { Length: > 0 })
        {
            await using var stream = Input.ImageUpload.OpenReadStream();
            await _media.UploadAsync(
                stream, Input.ImageUpload.FileName, MediaOwnerType.Article, articleId, authorId, null);
        }

        // Weitere Bilder für den Textlauf (Verwaltung mit Markdown-Snippets im Edit-Dialog).
        foreach (var file in Input.ContentImageUploads)
        {
            if (file is null || file.Length == 0)
            {
                continue;
            }

            await using var stream = file.OpenReadStream();
            await _media.UploadAsync(
                stream, file.FileName, MediaOwnerType.Article, articleId, authorId, null);
        }
    }
}
