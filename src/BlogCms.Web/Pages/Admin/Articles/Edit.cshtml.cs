using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using BlogCms.Web.Content;
using BlogCms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Admin.Articles;

/// <summary>
/// Presentation only: loads the article, enforces ownership (FeatureFix1 BR-013),
/// manages the image section and delegates saving to IArticlePostingService.
/// </summary>
[Authorize(Policy = Policies.RequireAuthor)]
public class EditModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly IArticlePostingService _posting;
    private readonly IMediaService _media;
    private readonly UserManager<User> _userManager;

    public EditModel(
        IArticleService articles,
        IArticlePostingService posting,
        IMediaService media,
        UserManager<User> userManager)
    {
        _articles = articles;
        _posting = posting;
        _media = media;
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

        var result = await _posting.UpdateAsync(id, Input.ToRequest(Input.Status), CurrentUserId);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            HasExistingImage = article.MediaAssets.Count > 0;
            await LoadImagesAsync(id);
            return Page();
        }

        TempData["Message"] = ArticleMessages.ForUpdate(article);
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
