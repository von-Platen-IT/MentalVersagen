using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Models.Api;

/// <summary>
/// Typ-URLs der ProblemDetails-Fehlerarten der Management-API. Die einheitlichen
/// Typen machen Fehler für KI-Clients maschinell unterscheidbar.
/// </summary>
public static class ProblemTypes
{
    public const string Validation = "https://mentalversagen.de/problems/validation-error";
    public const string Unauthorized = "https://mentalversagen.de/problems/unauthorized";
    public const string NotFound = "https://mentalversagen.de/problems/not-found";
    public const string RateLimit = "https://mentalversagen.de/problems/rate-limit";
    public const string NotConfigured = "https://mentalversagen.de/problems/not-configured";
    public const string Internal = "https://mentalversagen.de/problems/internal-error";
}

/// <summary>
/// Ein Feldfehler im Format der Management-API. Entspricht der
/// <c>PostError</c>-Struktur des Posting-Workflows (Feld + Meldung).
/// </summary>
public sealed record ApiFieldError(string Field, string Message);

/// <summary>
/// Erzeugt die einheitlichen ProblemDetails-Antworten der Management-API
/// (RFC 7807, <c>application/problem+json</c>). Validierungsfehler liegen als
/// <c>errors[]</c> mit <c>field</c>/<c>message</c> vor — so können KI-Clients
/// fehlerhafte Felder direkt erkennen und korrigieren.
/// </summary>
public static class ApiProblems
{
    /// <summary>JSON-Optionen im Web-Format (camelCase) für konsistente API-Antworten.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Schreibt eine ProblemDetails-Antwort im RFC-7807-Format
    /// (<c>application/problem+json</c>, camelCase) direkt in die Response — für
    /// Stellen außerhalb des MVC-Pipelines (Auth-Challenge, Rate-Limit, Exception-Handler).
    /// </summary>
    public static Task WriteAsync(
        HttpResponse response, ProblemDetails problem, CancellationToken cancellationToken = default) =>
        response.WriteAsJsonAsync(problem, JsonOptions, "application/problem+json", cancellationToken);

    /// <summary>400 mit Feldfehlerliste.</summary>
    public static ProblemDetails Validation(IEnumerable<ApiFieldError> errors)
    {
        var problem = new ProblemDetails
        {
            Type = ProblemTypes.Validation,
            Title = "Die Eingaben sind ungültig.",
            Status = StatusCodes.Status400BadRequest
        };
        problem.Extensions["errors"] = errors.ToList();
        return problem;
    }

    /// <summary>401 — fehlender oder ungültiger API-Key.</summary>
    public static ProblemDetails Unauthorized(string detail) => new()
    {
        Type = ProblemTypes.Unauthorized,
        Title = "Nicht authentifiziert.",
        Status = StatusCodes.Status401Unauthorized,
        Detail = detail
    };

    /// <summary>404 — die angeforderte Ressource existiert nicht.</summary>
    public static ProblemDetails NotFound(string detail) => new()
    {
        Type = ProblemTypes.NotFound,
        Title = "Ressource nicht gefunden.",
        Status = StatusCodes.Status404NotFound,
        Detail = detail
    };

    /// <summary>429 — zu viele Anfragen (Rate-Limit).</summary>
    public static ProblemDetails RateLimitExceeded() => new()
    {
        Type = ProblemTypes.RateLimit,
        Title = "Zu viele Anfragen.",
        Status = StatusCodes.Status429TooManyRequests,
        Detail = "Das Anfragen-Limit wurde überschritten. Bitte später erneut versuchen."
    };

    /// <summary>
    /// 503 — die API ist unvollständig konfiguriert (z. B. fehlende
    /// <c>Api:AuthorId</c> für das Anlegen von Artikeln).
    /// </summary>
    public static ProblemDetails NotConfigured(string detail) => new()
    {
        Type = ProblemTypes.NotConfigured,
        Title = "API nicht vollständig konfiguriert.",
        Status = StatusCodes.Status503ServiceUnavailable,
        Detail = detail
    };

    /// <summary>500 — unbehandelter Serverfehler.</summary>
    public static ProblemDetails Internal() => new()
    {
        Type = ProblemTypes.Internal,
        Title = "Interner Fehler.",
        Status = StatusCodes.Status500InternalServerError,
        Detail = "Beim Verarbeiten der Anfrage ist ein unerwarteter Fehler aufgetreten."
    };
}
