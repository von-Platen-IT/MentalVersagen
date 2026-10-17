using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Ratings;
using BlogCms.Web.Authorization;
using BlogCms.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogCms.Web.Pages.Articles;

public class DetailsModel : PageModel
{
    private readonly IArticleService _articles;
    private readonly ICommentService _comments;
    private readonly IMediaService _media;
    private readonly IMarkdownRenderer _markdownRenderer;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRatingService _ratings;
    private readonly IActivityLogService _activity;
    private readonly UserManager<User> _userManager;

    public DetailsModel(
        IArticleService articles,
        ICommentService comments,
        IMediaService media,
        IMarkdownRenderer markdownRenderer,
        IAuthorizationService authorizationService,
        IRatingService ratings,
        IActivityLogService activity,
        UserManager<User> userManager)
    {
        _articles = articles;
        _comments = comments;
        _media = media;
        _markdownRenderer = markdownRenderer;
        _authorizationService = authorizationService;
        _ratings = ratings;
        _activity = activity;
        _userManager = userManager;
    }

    public Article? Article { get; private set; }

    public bool IsPreview { get; private set; }

    public string ContentHtml { get; private set; } = string.Empty;

    public bool CanReadContent { get; private set; }

    public bool RequiresLogin { get; private set; }

    public bool RequiresPremium { get; private set; }

    public string? TitleImageUrl { get; private set; }

    /// <summary>Alt text for the title image (from the uploaded asset, if any).</summary>
    public string? TitleImageAlt { get; private set; }

    /// <summary>Id of the media asset used as title image (to exclude it from the gallery).</summary>
    public Guid? TitleImageAssetId { get; private set; }

    /// <summary>Article images for the thumbnail gallery (title image excluded).</summary>
    public IReadOnlyList<MediaAsset> GalleryImages =>
        Images.Where(i => i.Id != TitleImageAssetId).ToList();

    public IReadOnlyList<Comment> Comments { get; private set; } = [];

    public IReadOnlyList<MediaAsset> Images { get; private set; } = [];

    public RatingSummary ArticleRating { get; private set; } = RatingSummary.Empty;

    public RatingValue? UserArticleRating { get; private set; }

    public IReadOnlyDictionary<Guid, RatingSummary> CommentRatings { get; private set; } =
        new Dictionary<Guid, RatingSummary>();

    public IReadOnlyDictionary<Guid, RatingValue> UserCommentRatings { get; private set; } =
        new Dictionary<Guid, RatingValue>();

    public bool IsModerator => User.IsInRole(nameof(UserRole.Moderator)) || User.IsInRole(nameof(UserRole.Admin));

    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    public bool IsArticleAuthor => Article is not null && Article.AuthorId == CurrentUserId;

    /// <summary>Admins/moderators and the article author may moderate the comments of this article.</summary>
    public bool CanModerateComments => IsModerator || IsArticleAuthor;

    public bool IsCommentEditable(Comment comment) =>
        comment.DeletedAt is null &&
        comment.UserId == CurrentUserId &&
        DateTime.UtcNow <= comment.CreatedAt.AddMinutes(_comments.EditWindowMinutes);

    public string GetImageUrl(MediaAsset asset) => _media.GetUrl(asset);

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var article = await _articles.GetPublishedBySlugAsync(slug);
        if (article is null)
        {
            var previewCandidate = await _articles.GetBySlugForPreviewAsync(slug);
            if (previewCandidate is not null && (IsAdmin || (CurrentUserId != Guid.Empty && previewCandidate.AuthorId == CurrentUserId)))
            {
                article = previewCandidate;
                IsPreview = true;
            }
            else
            {
                return NotFound();
            }
        }

        Article = article;

        // Unified activity log: record the view only for published articles, not author previews.
        if (!IsPreview)
        {
            await _activity.LogArticleViewAsync(article.Id, CurrentUserIdOrNull, HttpContext.GetClientIp());
        }

        TitleImageUrl = BlogCms.Web.Content.ArticleDisplay.ResolveTitleImage(article, _media);

        // When the title image comes from an uploaded asset, remember which one
        // (alt text + exclusion from the thumbnail gallery below the article).
        if (string.IsNullOrWhiteSpace(article.TitleImageUrl))
        {
            var titleAsset = article.MediaAssets.FirstOrDefault(m => m.IsCover)
                             ?? article.MediaAssets.OrderBy(m => m.CreatedAt).FirstOrDefault();
            TitleImageAssetId = titleAsset?.Id;
            TitleImageAlt = titleAsset?.AltText;
        }

        await EvaluateAccessAsync(article);

        if (CanReadContent)
        {
            ContentHtml = _markdownRenderer.ToSafeHtml(article.ContentMarkdown);
        }

        Images = await _media.GetForOwnerAsync(MediaOwnerType.Article, article.Id);
        Comments = await _comments.GetThreadAsync(article.Id);

        ArticleRating = await _ratings.GetArticleSummaryAsync(article.Id);
        UserArticleRating = await _ratings.GetUserArticleRatingAsync(article.Id, CurrentUserId);

        var commentIds = Comments.Select(c => c.Id).ToList();
        CommentRatings = await _ratings.GetCommentSummariesAsync(commentIds);
        UserCommentRatings = await _ratings.GetUserCommentRatingsAsync(commentIds, CurrentUserId);

        return Page();
    }

    private async Task EvaluateAccessAsync(Article article)
    {
        if (IsAdmin || (CurrentUserId != Guid.Empty && article.AuthorId == CurrentUserId))
        {
            CanReadContent = true;
            return;
        }

        switch (article.AccessLevel)
        {
            case ArticleAccessLevel.Registered:
                CanReadContent = User.Identity?.IsAuthenticated == true;
                RequiresLogin = !CanReadContent;
                break;
            case ArticleAccessLevel.Premium:
                var premium = await _authorizationService.AuthorizeAsync(User, Policies.PremiumAccess);
                CanReadContent = premium.Succeeded;
                RequiresPremium = !CanReadContent;
                break;
            default:
                CanReadContent = true;
                break;
        }
    }

    public async Task<IActionResult> OnPostRateArticleAsync(string slug, RatingValue value)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Articles/Details", new { slug }) });
        }

        var article = await _articles.GetPublishedBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        await EvaluateAccessAsync(article);
        if (!CanReadContent)
        {
            return Forbid();
        }

        var result = await _ratings.RateArticleAsync(article.Id, CurrentUserId, value);

        // Nur abgegebene Bewertungen protokollieren, nicht das Zurücknehmen per Toggle.
        if (result.Succeeded && result.Rating is not null)
        {
            await _activity.LogArticleRatedAsync(
                article.Id, value, CurrentUserIdOrNull, HttpContext.GetClientIp());
        }

        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            !result.Succeeded
                ? result.Error
                : result.Rating is null ? "Bewertung zurückgenommen." : "Danke für deine Bewertung.";

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: "article-rating");
    }

    public async Task<IActionResult> OnPostRateCommentAsync(string slug, Guid commentId, RatingValue value)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Articles/Details", new { slug }) });
        }

        var result = await _ratings.RateCommentAsync(commentId, CurrentUserId, value);
        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            result.Succeeded ? "Danke für deine Bewertung." : result.Error;

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: $"comment-{commentId}");
    }

    public async Task<IActionResult> OnPostHighlightCommentAsync(string slug, Guid commentId, bool highlighted)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Articles/Details", new { slug }) });
        }

        // Server-side enforcement of the highlight rule happens inside the comment service.
        var result = await _comments.SetHighlightAsync(commentId, highlighted, CurrentUserId, IsAdmin);
        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            result.Succeeded
                ? (highlighted ? "Kommentar ausgezeichnet." : "Auszeichnung entfernt.")
                : result.Error;

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: $"comment-{commentId}");
    }

    public async Task<IActionResult> OnPostAddCommentAsync(
        string slug, string content, Guid? parentId, IFormFile? image)
    {
        var article = await _articles.GetPublishedBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Articles/Details", new { slug }) });
        }

        await EvaluateAccessAsync(article);
        if (!CanReadContent)
        {
            return Forbid();
        }

        var userId = CurrentUserId;
        var result = await _comments.AddAsync(article.Id, userId, content, parentId);

        if (result.Succeeded && result.Comment is not null)
        {
            await _activity.LogCommentCreatedAsync(
                article.Id, result.Comment.Id, CurrentUserIdOrNull, HttpContext.GetClientIp());
        }

        string? uploadError = null;
        if (result.Succeeded && result.Comment is not null && image is { Length: > 0 })
        {
            await using var stream = image.OpenReadStream();
            var upload = await _media.UploadAsync(
                stream, image.FileName, MediaOwnerType.Comment, result.Comment.Id, userId, null);

            if (!upload.Succeeded)
            {
                uploadError = upload.Error;
            }
        }

        // Ein fehlgeschlagener Bild-Upload (z. B. über 4 MB) muss sichtbar bleiben
        // und darf nicht von der Erfolgsmeldung überschrieben werden.
        if (uploadError is not null)
        {
            TempData["CommentError"] = uploadError;
        }
        else if (result.Succeeded && result.Comment is not null)
        {
            TempData["CommentMessage"] = result.Comment.Status == CommentStatus.Approved
                ? "Kommentar gespeichert."
                : "Dein Kommentar wurde gespeichert und wird vor der Veröffentlichung durch die Moderation geprüft.";
        }
        else
        {
            TempData["CommentError"] = result.Error;
        }

        var fragment = result.Succeeded && result.Comment is { Status: CommentStatus.Approved }
            ? $"comment-{result.Comment.Id}"
            : "comments";

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: fragment);
    }

    public async Task<IActionResult> OnPostEditCommentAsync(string slug, Guid commentId, string content)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await _comments.EditAsync(commentId, CurrentUserId, content);
        if (result.Succeeded && result.Comment is not null)
        {
            TempData["CommentMessage"] = result.Comment.Status == CommentStatus.Approved
                ? "Kommentar aktualisiert."
                : "Dein bearbeiteter Kommentar wurde zur Prüfung an die Moderation übergeben.";
        }
        else
        {
            TempData["CommentError"] = result.Error;
        }

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: $"comment-{commentId}");
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(string slug, Guid commentId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        // CommentService checks comment ownership, article ownership (BR-063) and moderator flag.
        var deleted = await _comments.SoftDeleteAsync(commentId, CurrentUserId, IsModerator);
        TempData[deleted ? "CommentMessage" : "CommentError"] =
            deleted ? "Kommentar gelöscht." : "Löschen nicht möglich.";

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: "comments");
    }

    public async Task<IActionResult> OnPostReportCommentAsync(string slug, Guid commentId, ReportReason reason, string? note)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var reported = await _comments.ReportAsync(commentId, CurrentUserId, reason, note);
        TempData[reported ? "CommentMessage" : "CommentError"] =
            reported ? "Danke, die Meldung wurde übermittelt." : "Meldung konnte nicht erstellt werden.";

        return RedirectToPage("/Articles/Details", pageHandler: null, routeValues: new { slug }, fragment: $"comment-{commentId}");
    }

    private async Task<Guid> ResolveArticleIdAsync(string slug)
    {
        var article = Article ?? await _articles.GetPublishedBySlugAsync(slug);
        return article?.Id ?? Guid.Empty;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;

    /// <summary>Account id for the activity log, or <c>null</c> for anonymous visitors.</summary>
    private Guid? CurrentUserIdOrNull => CurrentUserId == Guid.Empty ? null : CurrentUserId;
}
