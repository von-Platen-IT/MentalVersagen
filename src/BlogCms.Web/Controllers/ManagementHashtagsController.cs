using BlogCms.Infrastructure.Content;
using BlogCms.Web.Configuration;
using BlogCms.Web.Models.Api;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Management-API für Hashtags: Auflisten aller Hashtags inklusive
/// Nutzungszahl. Das Ändern der Hashtags eines Artikels erfolgt über
/// <c>PUT/DELETE /api/v1/articles/{id}/hashtags</c>.
/// </summary>
[ApiController]
[Route("api/v1/hashtags")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[EnableRateLimiting(ApiOptions.RateLimitPolicy)]
[Produces("application/json")]
public sealed class ManagementHashtagsController : ControllerBase
{
    private readonly IArticleService _articles;

    public ManagementHashtagsController(IArticleService articles)
    {
        _articles = articles;
    }

    /// <summary>
    /// Listet alle Hashtags mit ihrer Gesamtnutzungszahl, absteigend nach
    /// Häufigkeit sortiert.
    /// </summary>
    /// <response code="200">Liste aller Hashtags.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    [HttpGet(Name = "listHashtags")]
    [ProducesResponseType(typeof(IReadOnlyList<HashtagUsage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListHashtags(CancellationToken cancellationToken)
    {
        var items = await _articles.GetHashtagUsageAsync(cancellationToken);
        return Ok(items);
    }
}
