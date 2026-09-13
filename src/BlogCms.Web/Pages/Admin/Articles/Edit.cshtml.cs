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
    private readonly UserManager<Domain.Entities.User> _userManager;

    public EditModel(
        IArticleService articles,
        IMediaService media,
        IOEmbedService oEmbed,
        UserManager<Domain.Entities.User> userManager)
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
            var uploadedBy = article.AuthorId;
            await _media.UploadAsync(
                stream, Input.ImageUpload.FileName, MediaOwnerType.Article, article.Id, uploadedBy, null);
        }

        TempData["Message"] = $"Artikel „{article.Title}“ wurde aktualisiert.";
        return RedirectToPage("Index");
    }

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
}
