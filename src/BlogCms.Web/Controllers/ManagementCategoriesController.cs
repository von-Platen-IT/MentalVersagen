using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Categories;
using BlogCms.Web.Configuration;
using BlogCms.Web.Models.Api;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogCms.Web.Controllers;

/// <summary>
/// Management-API zur Pflege der dynamischen Kategorien: Auflisten, Lesen,
/// Anlegen, Bearbeiten und Löschen. Zugriff wie bei der Artikel-API über den
/// statischen API-Key (<c>X-Api-Key</c>).
/// </summary>
/// <remarks>
/// <para>Die Systemkategorie <c>undefined</c> ist geschützt: Sie kann weder
/// umbenannt noch gelöscht werden (Antwort <c>409 Conflict</c>). Beim Löschen
/// einer regulären Kategorie werden ihre Artikel automatisch der Kategorie
/// <c>undefined</c> zugeordnet.</para>
/// <para>Die Route <c>{idOrSlug}</c> akzeptiert sowohl die UUID als auch den
/// Slug einer Kategorie.</para>
/// </remarks>
[ApiController]
[Route("api/v1/categories")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[EnableRateLimiting(ApiOptions.RateLimitPolicy)]
[Produces("application/json")]
public sealed class ManagementCategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    private readonly ILogger<ManagementCategoriesController> _logger;

    public ManagementCategoriesController(
        ICategoryService categories,
        ILogger<ManagementCategoriesController> logger)
    {
        _categories = categories;
        _logger = logger;
    }

    /// <summary>
    /// Listet alle Kategorien inklusive Artikelanzahl. Reguläre Kategorien stehen
    /// vor der Systemkategorie <c>undefined</c>.
    /// </summary>
    /// <response code="200">Liste aller Kategorien.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    [HttpGet(Name = "listCategories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryReadModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListCategories(CancellationToken cancellationToken)
    {
        var usage = await _categories.GetUsageAsync(cancellationToken);
        var items = usage
            .Select(u => CategoryReadModel.FromEntity(u.Category, u.ArticleCount))
            .ToList();
        return Ok(items);
    }

    /// <summary>
    /// Liest eine einzelne Kategorie anhand ihrer UUID oder ihres Slugs.
    /// </summary>
    /// <param name="idOrSlug">UUID oder Slug der Kategorie.</param>
    /// <param name="cancellationToken">Bricht die Abfrage bei Client-Abbruch ab.</param>
    /// <response code="200">Die Kategorie inklusive Artikelanzahl.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Keine Kategorie mit dieser UUID/diesem Slug.</response>
    [HttpGet("{idOrSlug}", Name = "getCategory")]
    [ProducesResponseType(typeof(CategoryReadModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(
        string idOrSlug, CancellationToken cancellationToken)
    {
        var category = await ResolveAsync(idOrSlug, cancellationToken);
        if (category is null)
        {
            return NotFound(ApiProblems.NotFound(
                $"Die Kategorie „{idOrSlug}“ wurde nicht gefunden."));
        }

        var count = await _categories.CountArticlesAsync(category.Id, cancellationToken);
        return Ok(CategoryReadModel.FromEntity(category, count));
    }

    /// <summary>
    /// Legt eine neue Kategorie an. Der Slug wird bei Bedarf aus dem Namen
    /// generiert; der Slug <c>undefined</c> ist der Systemkategorie vorbehalten.
    /// </summary>
    /// <param name="model">Kategoriedaten (Name, optionaler Slug, Beschreibung, Disclaimer, Sortierung).</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="201">Kategorie wurde angelegt; <c>Location</c> zeigt auf <c>getCategory</c>.</response>
    /// <response code="400">Validierungsfehler — Details in <c>errors[]</c>.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    [HttpPost(Name = "createCategory")]
    [ProducesResponseType(typeof(CategoryReadModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CategoryWriteModel model, CancellationToken cancellationToken)
    {
        var result = await _categories.CreateAsync(ToInput(model), cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiProblems.Validation(
                [new ApiFieldError("name", result.Error ?? "Ungültige Eingabe.")]));
        }

        var readModel = CategoryReadModel.FromEntity(result.Category!);
        _logger.LogInformation("API: Kategorie {CategoryId} angelegt.", readModel.Id);
        return CreatedAtAction(
            nameof(GetCategory), new { idOrSlug = readModel.Id }, readModel);
    }

    /// <summary>
    /// Bearbeitet eine bestehende Kategorie. Der Slug bleibt stabil, solange kein
    /// neuer Slug übergeben wird — so bleiben öffentliche Filter-URLs gültig.
    /// </summary>
    /// <param name="idOrSlug">UUID oder Slug der Kategorie.</param>
    /// <param name="model">Vollständige Kategoriedaten.</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="200">Die aktualisierte Kategorie.</response>
    /// <response code="400">Validierungsfehler — Details in <c>errors[]</c>.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Keine Kategorie mit dieser UUID/diesem Slug.</response>
    /// <response code="409">Die Systemkategorie <c>undefined</c> kann nicht umbenannt werden.</response>
    [HttpPut("{idOrSlug}", Name = "updateCategory")]
    [ProducesResponseType(typeof(CategoryReadModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(
        string idOrSlug, [FromBody] CategoryWriteModel model,
        CancellationToken cancellationToken)
    {
        var category = await ResolveAsync(idOrSlug, cancellationToken);
        if (category is null)
        {
            return NotFound(ApiProblems.NotFound(
                $"Die Kategorie „{idOrSlug}“ wurde nicht gefunden."));
        }

        var result = await _categories.UpdateAsync(category.Id, ToInput(model), cancellationToken);
        if (!result.Succeeded)
        {
            var error = result.Error ?? "Ungültige Eingabe.";
            if (error.Contains("nicht gefunden", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiProblems.NotFound(error));
            }
            if (error.Contains("Systemkategorie", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(ApiProblems.Conflict(error));
            }
            return BadRequest(ApiProblems.Validation([new ApiFieldError("name", error)]));
        }

        var count = await _categories.CountArticlesAsync(result.Category!.Id, cancellationToken);
        _logger.LogInformation("API: Kategorie {CategoryId} bearbeitet.", result.Category!.Id);
        return Ok(CategoryReadModel.FromEntity(result.Category!, count));
    }

    /// <summary>
    /// Löscht eine Kategorie. Ihre Artikel werden vorher der Systemkategorie
    /// <c>undefined</c> zugeordnet. Die Systemkategorie selbst ist geschützt.
    /// </summary>
    /// <param name="idOrSlug">UUID oder Slug der Kategorie.</param>
    /// <param name="cancellationToken">Bricht die Verarbeitung bei Client-Abbruch ab.</param>
    /// <response code="204">Kategorie wurde gelöscht.</response>
    /// <response code="401">Fehlender oder ungültiger API-Key.</response>
    /// <response code="404">Keine Kategorie mit dieser UUID/diesem Slug.</response>
    /// <response code="409">Die Systemkategorie <c>undefined</c> kann nicht gelöscht werden.</response>
    [HttpDelete("{idOrSlug}", Name = "deleteCategory")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(
        string idOrSlug, CancellationToken cancellationToken)
    {
        var category = await ResolveAsync(idOrSlug, cancellationToken);
        if (category is null)
        {
            return NotFound(ApiProblems.NotFound(
                $"Die Kategorie „{idOrSlug}“ wurde nicht gefunden."));
        }

        if (category.IsSystem)
        {
            return Conflict(ApiProblems.Conflict(
                "Die Systemkategorie „undefined“ kann nicht gelöscht werden."));
        }

        var result = await _categories.DeleteAsync(category.Id, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiProblems.Validation(
                [new ApiFieldError("id", result.Error ?? "Löschen fehlgeschlagen.")]));
        }

        _logger.LogInformation("API: Kategorie {CategoryId} gelöscht.", category.Id);
        return NoContent();
    }

    /// <summary>Löst <c>{idOrSlug}</c> zuerst als UUID, dann als Slug auf.</summary>
    private async Task<Category?> ResolveAsync(
        string idOrSlug, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrSlug, out var id))
        {
            var byId = await _categories.GetByIdAsync(id, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        return await _categories.GetBySlugAsync(idOrSlug, cancellationToken);
    }

    private static CategoryInput ToInput(CategoryWriteModel model) =>
        new(
            Name: model.Name ?? string.Empty,
            Slug: model.Slug,
            Description: model.Description,
            Disclaimer: model.Disclaimer,
            DisplayOrder: model.DisplayOrder,
            Color: model.Color);
}
