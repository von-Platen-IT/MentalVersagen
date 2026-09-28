using BlogCms.Infrastructure.Content;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Server-side Markdown preview for the editor. Uses the same renderer as the
/// public article page, so the preview matches the published output exactly
/// (Markdig + HtmlSanitizer).
/// </summary>
[Route("api/markdown")]
[Authorize(Policy = Policies.RequireAuthor)]
[ValidateAntiForgeryToken]
public class MarkdownController : Controller
{
    private readonly IMarkdownRenderer _markdownRenderer;

    public MarkdownController(IMarkdownRenderer markdownRenderer)
    {
        _markdownRenderer = markdownRenderer;
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromForm] string? markdown)
    {
        return Json(new { html = _markdownRenderer.ToSafeHtml(markdown) });
    }
}
