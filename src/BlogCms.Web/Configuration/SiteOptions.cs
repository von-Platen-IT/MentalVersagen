namespace BlogCms.Web.Configuration;

/// <summary>
/// Öffentliche Adresse der Anwendung. Bindet an die Konfigurationssektion
/// <see cref="SectionName"/> ("Site"), die über appsettings*.json, Umgebungsvariablen
/// (z. B. <c>Site__PublicBaseUrl</c>) oder die lokal geladene <c>.env</c>-Datei
/// gesetzt werden kann.
///
/// Hintergrund: Absolute URLs (RSS-Feed, Bestätigungs- und Abmelde-Links in
/// E-Mails) werden aus Schema und Host des HTTP-Requests abgeleitet. Hinter
/// einem Reverse-Proxy sieht die Anwendung dabei nur den internen Host
/// ("http://blogcms-app:8080"). Mit <see cref="PublicBaseUrl"/> wird die
/// öffentliche Adresse fest vorgegeben, mit <see cref="UseForwardedHeaders"/>
/// werden die Proxy-Header (X-Forwarded-Proto/Host) ausgewertet.
/// </summary>
public class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>
    /// Öffentliche Basis-URL der Anwendung, z. B. "https://mentalversagen.de".
    /// Leer (Standard) = die Adresse wird aus dem HTTP-Request abgeleitet
    /// (Scheme + Host inkl. Forwarded-Headern). Es wird nur der Ursprung
    /// (Schema + Host) verwendet — ein Pfad-Präfix ist nicht vorgesehen.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// X-Forwarded-Proto/Host/For auswerten (Standard: true). Nötig, wenn die
    /// Anwendung hinter einem Reverse Proxy (nginx, Traefik, Caddy) läuft.
    /// Achtung: Ohne <see cref="KnownProxies"/> wird jedem Proxy vertraut —
    /// die Anwendung sollte dann nicht direkt aus dem Internet erreichbar sein.
    /// </summary>
    public bool UseForwardedHeaders { get; set; } = true;

    /// <summary>
    /// Vertrauenswürdige Proxy-IP-Adressen (Komma-getrennt konfigurierbar).
    /// Leer = alle Proxies werden vertraut (typisch für Container-Netze, in
    /// denen die Proxy-IP wechseln kann).
    /// </summary>
    public string[] KnownProxies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Normalisierte öffentliche Basis-URL ohne Trailing-Slash, z.B.
    /// "https://mentalversagen.de". Leer, wenn keine (gültige) URL
    /// konfiguriert ist — dann wird der Request ausgewertet.
    /// </summary>
    public string NormalizedPublicBaseUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(PublicBaseUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return string.Empty;
            }

            // Nur der Ursprung (Schema + Host + Port) — Pfade und Queries
            // werden bewusst verworfen, damit alle absoluten URLs dieselbe
            // Basis teilen.
            return $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
        }
    }

    /// <summary>True, wenn eine gültige öffentliche Basis-URL konfiguriert ist.</summary>
    public bool HasPublicBaseUrl => NormalizedPublicBaseUrl.Length > 0;

    /// <summary>
    /// Der kanonische Host der Website: der Host OHNE "www."-Präfix.
    ///
    /// <para>Hintergrund: Die Site ist über "mentalversagen.de" und
    /// "www.mentalversagen.de" erreichbar, beide liefern HTTP 200 mit
    /// identischem Inhalt. Für Suchmaschinen ist das Doppel-Inhalt — sie
    /// splitten die Linkkraft auf beide Varianten. Deshalb wird überall die
    /// www-lose Variante als kanonisch ausgegeben (Canonical, og:url,
    /// Sitemap). Der Host wird NICHT umgeschrieben — die Umleitung selbst
    /// macht der Reverse Proxy; die Anwendung markiert nur die richtige
    /// Variante als kanonisch.</para>
    /// </summary>
    public const string WwwPrefix = "www.";

    /// <summary>
    /// Entfernt ein "www."-Präfix vom Host. Wird auf den Host eines
    /// Requests angewendet, um die kanonische Variante zu bestimmen.
    /// </summary>
    public static string StripWww(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return host;
        }

        var trimmed = host.Trim();
        if (trimmed.StartsWith(WwwPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[WwwPrefix.Length..];
        }

        return trimmed;
    }
}
