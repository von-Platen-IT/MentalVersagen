using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Ratings;
using BlogCms.Web.Authorization;
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
    private readonly UserManager<User> _userManager;

    public DetailsModel(
        IArticleService articles,
        ICommentService comments,
        IMediaService media,
        IMarkdownRenderer markdownRenderer,
        IAuthorizationService authorizationService,
        IRatingService ratings,
        UserManager<User> userManager)
    {
        _articles = articles;
        _comments = comments;
        _media = media;
        _markdownRenderer = markdownRenderer;
        _authorizationService = authorizationService;
        _ratings = ratings;
        _userManager = userManager;
    }

    public Article? Article { get; private set; }

    public string ContentHtml { get; private set; } = string.Empty;

    public bool CanReadContent { get; private set; }

    public bool RequiresLogin { get; private set; }

    public bool RequiresPremium { get; private set; }

    public string? TitleImageUrl { get; private set; }

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

    public string GetImageUrl(MediaAsset asset) => _media.GetUrl(asset);

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var article = await _articles.GetPublishedBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        Article = article;
        TitleImageUrl = BlogCms.Web.Content.ArticleDisplay.ResolveTitleImage(article, _media);

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

        var result = await _ratings.RateArticleAsync(articleId: await ResolveArticleIdAsync(slug), CurrentUserId, value);
        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            result.Succeeded ? "Danke für deine Bewertung." : result.Error;

        return RedirectToPage("/Articles/Details", new { slug });
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

        return RedirectToPage("/Articles/Details", new { slug });
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

        return RedirectToPage("/Articles/Details", new { slug });
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

        var userId = CurrentUserId;
        var result = await _comments.AddAsync(article.Id, userId, content, parentId);

        if (result.Succeeded && result.Comment is not null && image is { Length: > 0 })
        {
            await using var stream = image.OpenReadStream();
            var upload = await _media.UploadAsync(
                stream, image.FileName, MediaOwnerType.Comment, result.Comment.Id, userId, null);

            if (!upload.Succeeded)
            {
                TempData["CommentError"] = upload.Error;
            }
        }

        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            result.Succeeded ? "Kommentar gespeichert." : result.Error;

        return RedirectToPage("/Articles/Details", new { slug });
    }

    public async Task<IActionResult> OnPostEditCommentAsync(string slug, Guid commentId, string content)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await _comments.EditAsync(commentId, CurrentUserId, content);
        TempData[result.Succeeded ? "CommentMessage" : "CommentError"] =
            result.Succeeded ? "Kommentar aktualisiert." : result.Error;

        return RedirectToPage("/Articles/Details", new { slug });
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(string slug, Guid commentId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var deleted = await _comments.SoftDeleteAsync(commentId, CurrentUserId, CanModerateComments);
        TempData[deleted ? "CommentMessage" : "CommentError"] =
            deleted ? "Kommentar gelöscht." : "Löschen nicht möglich.";

        return RedirectToPage("/Articles/Details", new { slug });
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

        return RedirectToPage("/Articles/Details", new { slug });
    }

    private async Task<Guid> ResolveArticleIdAsync(string slug)
    {
        var article = Article ?? await _articles.GetPublishedBySlugAsync(slug);
        return article?.Id ?? Guid.Empty;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : Guid.Empty;
}
