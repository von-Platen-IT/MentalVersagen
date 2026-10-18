using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using BlogCms.Web.Configuration;
using BlogCms.Web.Models.Api;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Management-API zur Pflege der Blog-Artikel: Anlegen, Bearbeiten, Löschen und
/// Lesen. Der Zugriff erfolgt über einen statischen API-Key
/// (Header <c>X-Api-Key</c>) und ist auf die Fernsteuerung durch Skripte und
/// KI-Clients ausgelegt. Die fachlichen Regeln des Beitrags-Workflows greifen
/// identisch zur Web-Oberfläche.
/// </summary>
/// <remarks>
/// <para><b>Ablauf-Tipps für automatisierte Clients:</b> Zuerst
/// <c>listArticles</c>/<c>getArticle</c> aufrufen, um IDs und Slugs zu finden;
/// Änderungen über <c>updateArticle</c> (Voll-Update) vornehmen;
/// <c>deleteArticle</c> führt einen Soft-Delete aus (der Artikel bleibt in der
/// Datenbank erhalten, verschwindet aber aus allen Auflistungen).</para>
/// <para><b>Fehlerbehandlung:</b> Alle Fehler antworten als
/// <c>application/problem+json</c>. Validierungsfehler (Status 400) enthalten
/// <c>errors[]</c> mit <c>field</c>/<c>message</c> und benennen die korrigierbaren
/// Felder direkt.</para>
/// </remarks>
[ApiController]
[Route("api/v1/articles")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[EnableRateLimiting(ApiOptions.RateLimitPolicy)]
[Produces("application/json")]
public sealed class ManagementArticlesController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly IArticlePostingService _posting;
    private readonly IArticleService _articles;
    private readonly ApiOptions _apiOptions;
    private readonly ILogger<ManagementArticlesController> _logger;

    public ManagementArticlesController(
        IArticlePostingService posting,
        IArticleService articles,
        IOptions<ApiOptions> apiOptions,
        ILogger<ManagementArticlesController> logger)
    {
        _posting = posting;
        _articles = articles;
        _apiOptions = apiOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Listet Artikel des Blogs inklusive Entwürfe und geplanter Beiträge,
    /// absteigend nach letzter Änderung sortiert.
    /// </summary>
    /// <param name="status">Optional: nur Artikel in diesem Status
    /// (<c>Draft</c>, <c>Scheduled</c>, <c>Published</c>, <c>Archived</c>).</param>
    /// <param name="query">Optional: Suchbegriff in Titel, Slug und Kurzbeschreibung.</param>
    /// <param name="page">Seitennummer (1-basiert, Standard 1).</param>
    /// <param name="pageSize">Elemente pro Seite (Standard 20, maximal 50).</param>
    /// <param name="cancellationToken">Bricht die Abfrage bei Client-Abbruch ab.</param>
    /// <response code="200">Paginierte Artikelliste.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="429">Anfragen-Limit überschritten.</response>
    [HttpGet(Name = "listArticles")]
    [ProducesResponseType(typeof(PagedResponse<ArticleReadModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ListArticles(
        [FromQuery] ArticleStatus? status = null,
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var currentPage = Math.Max(1, page);
        var currentPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var all = await _articles.GetForManagementAsync(
            authorId: null, statusFilter: status, search: query, cancellationToken);

        var items = all
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(ArticleReadModel.FromEntity)
            .ToList();

        return Ok(new PagedResponse<ArticleReadModel>(items, all.Count, currentPage, currentPageSize));
    }

    /// <summary>
    /// Liest einen einzelnen Artikel inklusive Markdown-Quelltext, Tags, Hashtags
    /// und Medien-IDs.
    /// </summary>
    /// <param name="id">UUID des Artikels.</param>
    /// <param name="cancellationToken">Bricht die Abfrage bei Client-Abbruch ab.</param>
    /// <response code="200">Der vollständige Artikel.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Kein Artikel mit dieser ID.</response>
    [HttpGet("{id:guid}", Name = "getArticle")]
    [ProducesResponseType(typeof(ArticleReadModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(Guid id, CancellationToken cancellationToken)
    {
        var article = await _articles.GetByIdAsync(id, cancellationToken);
        if (article is null)
        {
            return NotFound(ApiProblems.NotFound($"Der Artikel {id} wurde nicht gefunden."));
        }

        return Ok(ArticleReadModel.FromEntity(article));
    }

    /// <summary>
    /// Legt einen neuen Artikel an. Der Artikel wird dem konfigurierten API-Benutzer
    /// (<c>Api:AuthorId</c>) als Autor zugeordnet. Der Antwort-Body enthält den
    /// gespeicherten Artikel inklusive vergebener <c>id</c> und <c>slug</c>.
    /// </summary>
    /// <remarks>
    /// Für <c>status = Published</c> oder <c>Scheduled</c> sind <c>excerpt</c>,
    /// <c>contentMarkdown</c> und eine Titelbild-Quelle Pflicht. Bei
    /// <c>status = Scheduled</c> muss <c>scheduledAt</c> in der Zukunft liegen.
    /// </remarks>
    /// <param name="model">Artikeldaten (Titel, Inhalt, Status, Metadaten).</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="201">Artikel wurde angelegt; <c>Location</c> zeigt auf <c>getArticle</c>.</response>
    /// <response code="400">Validierungsfehler — Details in <c>errors[]</c>.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="503"><c>Api:AuthorId</c> ist nicht konfiguriert.</response>
    [HttpPost(Name = "createArticle")]
    [ProducesResponseType(typeof(ArticleReadModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateArticle(
        [FromBody] ArticleWriteModel model, CancellationToken cancellationToken)
    {
        if (_apiOptions.AuthorId is not { } authorId)
        {
            _logger.LogWarning("API-Artikelanlage abgelehnt: Api:AuthorId ist nicht konfiguriert.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiProblems.NotConfigured(
                "Für das Anlegen von Artikeln muss Api:AuthorId auf einen bestehenden " +
                "Benutzer mit Rolle Author oder Admin gesetzt sein."));
        }

        var result = await _posting.CreateAsync(
            ToPostRequest(model), authorId,
            clientIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(ApiProblems.Validation(
                result.Errors.Select(e => new ApiFieldError(e.Field, e.Message))));
        }

        var readModel = ArticleReadModel.FromEntity(result.Article!);
        _logger.LogInformation("API: Artikel {ArticleId} angelegt ({Status}).", readModel.Id, readModel.Status);
        return CreatedAtAction(nameof(GetArticle), new { id = readModel.Id }, readModel);
    }

    /// <summary>
    /// Bearbeitet einen bestehenden Artikel vollständig (Voll-Update). Alle Felder
    /// werden aus dem Request übernommen; nicht gesendete Listenfelder werden geleert.
    /// Auch Statuswechsel (Veröffentlichen, Planen, Zurückziehen, Archivieren)
    /// erfolgen über diesen Endpunkt.
    /// </summary>
    /// <param name="id">UUID des zu bearbeitenden Artikels.</param>
    /// <param name="model">Vollständige Artikeldaten — Voll-Update aller Felder.</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="200">Der aktualisierte Artikel.</response>
    /// <response code="400">Validierungsfehler — Details in <c>errors[]</c>.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Kein Artikel mit dieser ID.</response>
    [HttpPut("{id:guid}", Name = "updateArticle")]
    [ProducesResponseType(typeof(ArticleReadModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateArticle(
        Guid id, [FromBody] ArticleWriteModel model, CancellationToken cancellationToken)
    {
        var existing = await _articles.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiProblems.NotFound($"Der Artikel {id} wurde nicht gefunden."));
        }

        // Die API handelt im Namen des konfigurierten API-Benutzers; ohne diesen
        // dient der ursprüngliche Autor als Akteur (Medienzuordnung).
        var actorId = _apiOptions.AuthorId ?? existing.AuthorId;

        var result = await _posting.UpdateAsync(id, ToPostRequest(model), actorId, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiProblems.Validation(
                result.Errors.Select(e => new ApiFieldError(e.Field, e.Message))));
        }

        var readModel = ArticleReadModel.FromEntity(result.Article!);
        _logger.LogInformation("API: Artikel {ArticleId} bearbeitet ({Status}).", readModel.Id, readModel.Status);
        return Ok(readModel);
    }

    /// <summary>
    /// Löscht einen Artikel (Soft-Delete: der Artikel bleibt in der Datenbank
    /// erhalten, ist aber öffentlich und in der Verwaltung nicht mehr sichtbar).
    /// </summary>
    /// <param name="id">UUID des zu löschenden Artikels.</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="204">Artikel wurde gelöscht.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Kein Artikel mit dieser ID.</response>
    [HttpDelete("{id:guid}", Name = "deleteArticle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteArticle(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _articles.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiProblems.NotFound($"Der Artikel {id} wurde nicht gefunden."));
        }

        await _articles.SoftDeleteAsync(id, cancellationToken);
        _logger.LogInformation("API: Artikel {ArticleId} gelöscht (Soft-Delete).", id);
        return NoContent();
    }

    /// <summary>
    /// Bildet das API-Schreibmodell auf den präsentationsunabhängigen
    /// Schreib-Request der Anwendungsschicht ab. Datei-Uploads sind der API
    /// bewusst nicht Teil des JSON-Workflows — Titelbilder erfolgen über
    /// <c>titleImageUrl</c> oder <c>uploadedImageIds</c>.
    /// </summary>
    private static ArticlePostRequest ToPostRequest(ArticleWriteModel model) =>
        new(
            Title: model.Title ?? string.Empty,
            Slug: model.Slug,
            TitleImageUrl: model.TitleImageUrl,
            Excerpt: model.Excerpt ?? string.Empty,
            ContentMarkdown: model.ContentMarkdown ?? string.Empty,
            Category: model.Category,
            AccessLevel: model.AccessLevel,
            Status: model.Status,
            ScheduledAt: model.ScheduledAt,
            Tags: model.Tags ?? [],
            Hashtags: model.Hashtags ?? [],
            VideoUrl: model.VideoUrl,
            CoverUpload: null,
            ContentUploads: [],
            UploadedImageIds: model.UploadedImageIds ?? []);
}
