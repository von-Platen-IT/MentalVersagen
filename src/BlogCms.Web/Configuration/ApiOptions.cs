namespace BlogCms.Web.Configuration;

/// <summary>
/// Konfiguration der Management-API (Artikel per API pflegen, siehe
/// plans/api-artikelverwaltung.md). Bindet an die Konfigurationssektion
/// <see cref="SectionName"/> ("Api"), die über appsettings*.json,
/// Umgebungsvariablen (z. B. <c>Api__Key</c>) oder die lokal geladene
/// <c>.env</c>-Datei gesetzt werden kann.
/// </summary>
public class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Name der Rate-Limit-Policy für die Management-API.</summary>
    public const string RateLimitPolicy = "api";

    /// <summary>
    /// Statischer API-Key für den Zugriff auf <c>/api/v1</c>. Leer = die API bleibt
    /// für jeden Zugriff geschlossen (Fail-Closed). Empfehlung: mindestens 32
    /// zufällige Bytes, z. B. erzeugt mit <c>openssl rand -hex 32</c>.
    /// Der Wert darf nie in eine committete Datei — nur in <c>.env</c> bzw. als
    /// Umgebungsvariable gesetzt werden.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Benutzer-ID (UUID), unter der über die API angelegte Artikel geführt werden.
    /// Der Benutzer muss existieren und die Rolle <c>Author</c> oder <c>Admin</c>
    /// haben. Ohne diesen Wert ist das Anlegen von Artikeln über die API nicht möglich.
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Sichtbarkeit der interaktiven Swagger-Oberfläche unter <c>/swagger</c>.
    /// Die maschinenlesbare OpenAPI-Spec unter <c>/swagger/v1/swagger.json</c> bleibt
    /// davon unberührt immer erreichbar — sie ist das Interface für KI-Clients.
    /// </summary>
    public bool SwaggerUiEnabled { get; set; }

    /// <summary>Maximale Anzahl API-Anfragen pro Minute und Client-IP.</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>True, wenn ein API-Key konfiguriert ist und die API genutzt werden kann.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key);
}
