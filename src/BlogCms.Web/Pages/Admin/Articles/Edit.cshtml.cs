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
public class EditModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IMediaService _media;
    private readonly IOEmbedService _oEmbed;
    private readonly UserManager<User> _userManager;

    public EditModel(
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

    public Guid ArticleId { get; private set; }

    public bool HasExistingImage { get; private set; }

    /// <summary>All images of this article (media management section below the form).</summary>
    public IReadOnlyList<MediaAsset> Images { get; private set; } = [];

    public string GetImageUrl(MediaAsset asset) => _media.GetUrl(asset);

    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    private Guid CurrentUserId =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return NotFound();
        }

        // Ownership: authors may only edit their own articles (FeatureFix1 BR-013).
        if (!IsAdmin && article.AuthorId != CurrentUserId)
        {
            return Forbid();
        }

        ArticleId = article.Id;
        HasExistingImage = article.MediaAssets.Count > 0;
        await LoadImagesAsync(article.Id);

        Input = new ArticleInputModel
        {
            Title = article.Title,
            Slug = article.Slug,
            TitleImageUrl = article.TitleImageUrl,
            ContentMarkdown = article.ContentMarkdown,
            Excerpt = article.Excerpt,
            Category = article.Category,
            AccessLevel = article.AccessLevel,
            Status = article.Status,
            ScheduledAt = article.ScheduledAt,
            Tags = string.Join(", ", article.ArticleTags.Select(at => at.Tag!.Name)),
            Hashtags = string.Join(", ", article.ArticleHashtags.Select(ah => ah.Hashtag!.Name)),
            VideoUrl = article.VideoEmbeds.FirstOrDefault()?.OriginalUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        ArticleId = id;

        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return NotFound();
        }

        if (!IsAdmin && article.AuthorId != CurrentUserId)
        {
            return Forbid();
        }

        HasExistingImage = article.MediaAssets.Count > 0;
        ValidateInput(HasExistingImage);

        if (!ModelState.IsValid)
        {
            await LoadImagesAsync(id);
            return Page();
        }

        article.Title = Input.Title;
        article.Slug = Input.Slug ?? string.Empty;
        article.TitleImageUrl = Input.TitleImageUrl;
        article.ContentMarkdown = Input.ContentMarkdown;
        article.Excerpt = Input.Excerpt;
        article.Category = Input.Category;
        article.AccessLevel = Input.AccessLevel;
        article.Status = Input.Status;
        article.ScheduledAt = Input.Status == ArticleStatus.Scheduled ? Input.ScheduledAt : null;

        await _articles.UpdateAsync(
            article, ArticleInputModel.ParseTags(Input.Tags), ArticleInputModel.ParseHashtags(Input.Hashtags));

        if (!string.IsNullOrWhiteSpace(Input.VideoUrl))
        {
            var resolved = await _oEmbed.ResolveAsync(Input.VideoUrl);
            await _articles.SetVideoEmbedAsync(article.Id, Input.VideoUrl, resolved);
        }

        if (Input.ImageUpload is { Length: > 0 })
        {
            await using var stream = Input.ImageUpload.OpenReadStream();
            await _media.UploadAsync(
                stream, Input.ImageUpload.FileName, MediaOwnerType.Article, article.Id, article.AuthorId, null);
        }

        await UploadContentImagesAsync(article);

        TempData["Message"] = $"Artikel „{article.Title}“ wurde aktualisiert.";
        return RedirectToPage("Index");
    }

    /// <summary>Sets an uploaded article image as the title image.</summary>
    public async Task<IActionResult> OnPostUseAsTitleAsync(Guid id, Guid imageId)
    {
        var (article, failure) = await LoadArticleOrFailureAsync(id);
        if (article is null)
        {
            return failure ?? NotFound();
        }

        var asset = await FindArticleImageAsync(id, imageId);
        if (asset is null)
        {
            return NotFound();
        }

        article.TitleImageUrl = _media.GetUrl(asset);
        await _articles.UpdateAsync(article, TagNames(article), HashtagNames(article));

        TempData["Message"] = "Das Bild wurde als Titelbild übernommen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Updates the alt text of an article image.</summary>
    public async Task<IActionResult> OnPostSaveImageAltAsync(Guid id, Guid imageId, string? altText)
    {
        var (article, failure) = await LoadArticleOrFailureAsync(id);
        if (article is null)
        {
            return failure ?? NotFound();
        }

        if (await FindArticleImageAsync(id, imageId) is null)
        {
            return NotFound();
        }

        await _media.SetAltTextAsync(imageId, altText);

        TempData["Message"] = "Der Alt-Text wurde gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Deletes an article image from storage and database.</summary>
    public async Task<IActionResult> OnPostDeleteImageAsync(Guid id, Guid imageId)
    {
        var (article, failure) = await LoadArticleOrFailureAsync(id);
        if (article is null)
        {
            return failure ?? NotFound();
        }

        if (await FindArticleImageAsync(id, imageId) is null)
        {
            return NotFound();
        }

        await _media.DeleteAsync(imageId);

        TempData["Message"] = "Das Bild wurde gelöscht.";
        return RedirectToPage(new { id });
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

    private async Task UploadContentImagesAsync(Article article)
    {
        foreach (var file in Input.ContentImageUploads)
        {
            if (file is null || file.Length == 0)
            {
                continue;
            }

            await using var stream = file.OpenReadStream();
            await _media.UploadAsync(
                stream, file.FileName, MediaOwnerType.Article, article.Id, article.AuthorId, null);
        }
    }

    private async Task LoadImagesAsync(Guid articleId)
    {
        Images = await _media.GetForOwnerAsync(MediaOwnerType.Article, articleId);
    }

    /// <summary>
    /// Loads the article and enforces ownership (FeatureFix1 BR-013).
    /// Returns the failure action (NotFound/Forbid) instead of the article when access is denied.
    /// </summary>
    private async Task<(Article? Article, IActionResult? Failure)> LoadArticleOrFailureAsync(Guid id)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null)
        {
            return (null, NotFound());
        }

        if (!IsAdmin && article.AuthorId != CurrentUserId)
        {
            return (null, Forbid());
        }

        return (article, null);
    }

    private async Task<MediaAsset?> FindArticleImageAsync(Guid articleId, Guid imageId)
    {
        var assets = await _media.GetForOwnerAsync(MediaOwnerType.Article, articleId);
        return assets.FirstOrDefault(m => m.Id == imageId);
    }

    private static IEnumerable<string> TagNames(Article article) =>
        article.ArticleTags.Select(at => at.Tag!.Name);

    private static IEnumerable<string> HashtagNames(Article article) =>
        article.ArticleHashtags.Select(ah => ah.Hashtag!.Name);
}
