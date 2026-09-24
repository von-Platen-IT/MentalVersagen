using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
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
    private readonly UserManager<User> _userManager;

    public CreateModel(
        IArticleService articles,
        IMediaService media,
        IOEmbedService oEmbed,
        UserManager<User> userManager)
    {
        _articles = articles;
        _media = media;
        _oEmbed = oEmbed;
        _userManager = userManager;
    }

    [BindProperty]
    public ArticleInputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        await ApplyMediaAsync(created.Id, authorId);

        TempData["Message"] = $"Artikel „{article.Title}“ wurde angelegt.";
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
