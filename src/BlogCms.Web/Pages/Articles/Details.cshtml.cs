using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
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
    private readonly UserManager<User> _userManager;

    public DetailsModel(
        IArticleService articles,
        ICommentService comments,
        IMediaService media,
        IMarkdownRenderer markdownRenderer,
        IAuthorizationService authorizationService,
        UserManager<User> userManager)
    {
        _articles = articles;
        _comments = comments;
        _media = media;
        _markdownRenderer = markdownRenderer;
        _authorizationService = authorizationService;
        _userManager = userManager;
    }

    public Article? Article { get; private set; }

    public string ContentHtml { get; private set; } = string.Empty;

    public bool CanReadContent { get; private set; }

    public IReadOnlyList<Comment> Comments { get; private set; } = [];

    public IReadOnlyList<MediaAsset> Images { get; private set; } = [];

    public bool IsModerator => User.IsInRole(nameof(UserRole.Moderator)) || User.IsInRole(nameof(UserRole.Admin));

    public string GetImageUrl(MediaAsset asset) => _media.GetUrl(asset);

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var article = await _articles.GetPublishedBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        Article = article;

        var premiumResult = await _authorizationService.AuthorizeAsync(User, Policies.PremiumAccess);
        CanReadContent = !article.IsPremium || premiumResult.Succeeded;

        if (CanReadContent)
        {
            ContentHtml = _markdownRenderer.ToSafeHtml(article.ContentMarkdown);
        }

        Images = await _media.GetForOwnerAsync(MediaOwnerType.Article, article.Id);
        Comments = await _comments.GetThreadAsync(article.Id);
        return Page();
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

        var userId = GetUserId();
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

        var result = await _comments.EditAsync(commentId, GetUserId(), content);
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

        var deleted = await _comments.SoftDeleteAsync(commentId, GetUserId(), IsModerator);
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

        var reported = await _comments.ReportAsync(commentId, GetUserId(), reason, note);
        TempData[reported ? "CommentMessage" : "CommentError"] =
            reported ? "Danke, die Meldung wurde übermittelt." : "Meldung konnte nicht erstellt werden.";

        return RedirectToPage("/Articles/Details", new { slug });
    }

    private Guid GetUserId()
    {
        var value = _userManager.GetUserId(User);
        return value is not null && Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
