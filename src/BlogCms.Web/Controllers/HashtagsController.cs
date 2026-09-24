using BlogCms.Infrastructure.Content;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// REST endpoint for the hashtag autocomplete (landing page and search page).
/// Returns every hashtag that is used by at least one published article,
/// ordered by usage count.
/// </summary>
[Route("api/hashtags")]
public class HashtagsController : Controller
{
    private readonly IArticleService _articles;

    public HashtagsController(IArticleService articles)
    {
        _articles = articles;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var hashtags = await _articles.GetPublishedHashtagsAsync(cancellationToken);

        return Json(hashtags.Select(h => new
        {
            name = h.Name,
            slug = h.Slug,
            count = h.Count
        }));
    }
}
