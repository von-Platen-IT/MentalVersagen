using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// AJAX upload endpoint for the Markdown editor. Images are stored as still
/// unassigned article assets (ArticleId = null) and claimed by the article
/// create/edit page on save (see IMediaService.AttachToArticleAsync).
/// </summary>
[Route("api/media")]
[Authorize(Policy = Policies.RequireAuthor)]
[ValidateAntiForgeryToken]
public class MediaController : Controller
{
    private readonly IMediaService _media;
    private readonly UserManager<User> _userManager;

    public MediaController(IMediaService media, UserManager<User> userManager)
    {
        _media = media;
        _userManager = userManager;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Keine Datei übermittelt." });
        }

        if (!Guid.TryParse(_userManager.GetUserId(User), out var userId))
        {
            return Unauthorized();
        }

        await using var stream = file.OpenReadStream();
        var result = await _media.UploadAsync(
            stream, file.FileName, MediaOwnerType.Article, Guid.Empty, userId, null, cancellationToken);

        if (!result.Succeeded || result.Asset is null)
        {
            return BadRequest(new { error = result.Error ?? "Upload fehlgeschlagen." });
        }

        return Json(new
        {
            id = result.Asset.Id,
            url = _media.GetUrl(result.Asset)
        });
    }
}
