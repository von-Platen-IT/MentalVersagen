using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BlogCms.Web.Configuration;
using BlogCms.Web.Models.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Security;

/// <summary>Namen und Konstanten des API-Key-Authentifizierungsschemas.</summary>
public static class ApiKeyDefaults
{
    /// <summary>Name des Authentication-Scheme für die Management-API.</summary>
    public const string AuthenticationScheme = "ApiKey";

    /// <summary>Header, der den statischen API-Key überträgt.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>
    /// Claim mit der Benutzer-ID, unter der über die API angelegte Artikel geführt
    /// werden (aus <see cref="ApiOptions.AuthorId"/>).
    /// </summary>
    public const string AuthorIdClaimType = "api_author_id";
}

/// <summary>
/// Authentifiziert Anfragen an die Management-API über einen statischen Key im
/// Header <see cref="ApiKeyDefaults.HeaderName"/>. Der Vergleich erfolgt
/// konstantzeitlich über SHA-256-Hashes, damit weder die Länge noch einzelne
/// Zeichenpositionen des gespeicherten Keys über Timing-Lecks nach außen fließen.
/// Ist kein Key konfiguriert, schlägt jeder Zugriff fehl (Fail-Closed) — die API
/// öffnet sich nie versehentlich. Der Key wird zu keinem Zeitpunkt geloggt.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiOptions _apiOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiOptions> apiOptions)
        : base(options, logger, encoder)
    {
        _apiOptions = apiOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_apiOptions.IsConfigured)
        {
            Logger.LogWarning(
                "API-Zugriff verweigert: kein Api:Key konfiguriert (Fail-Closed).");
            return Task.FromResult(AuthenticateResult.Fail(
                "Die Management-API ist nicht konfiguriert."));
        }

        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var providedValues))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"Der Header {ApiKeyDefaults.HeaderName} fehlt."));
        }

        var providedKey = providedValues.ToString().Trim();
        if (!KeysMatch(providedKey, _apiOptions.Key))
        {
            Logger.LogWarning("API-Zugriff verweigert: ungültiger API-Key.");
            return Task.FromResult(AuthenticateResult.Fail("Ungültiger API-Key."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "api"),
            new("auth_scheme", ApiKeyDefaults.AuthenticationScheme)
        };

        if (_apiOptions.AuthorId is { } authorId)
        {
            claims.Add(new Claim(ApiKeyDefaults.AuthorIdClaimType, authorId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Antwortet auf eine fehlgeschlagene Authentifizierung mit 401 und einer
    /// ProblemDetails-Struktur — auch Fehlerfälle haben damit ein für KI-Clients
    /// vorhersagbares Format.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        var problem = ApiProblems.Unauthorized(
            $"Für den Zugriff auf die Management-API muss ein gültiger API-Key über " +
            $"den Header {ApiKeyDefaults.HeaderName} übermittelt werden.");

        await ApiProblems.WriteAsync(Response, problem);
    }

    /// <summary>
    /// Konstantzeitlicher Key-Vergleich über SHA-256-Hashes: Beide Seiten werden
    /// gehasht und über <see cref="CryptographicOperations.FixedTimeEquals"/>
    /// verglichen — unabhängig von der tatsächlichen Key-Länge.
    /// </summary>
    private static bool KeysMatch(string providedKey, string configuredKey)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
