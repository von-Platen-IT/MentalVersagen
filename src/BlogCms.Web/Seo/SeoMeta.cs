using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Html;

namespace BlogCms.Web.Seo;

/// <summary>
/// Hilfsfunktionen für die SEO-Metadaten im <c>&lt;head&gt;</c>.
///
/// <para>Die Seiten setzen ihre Metadaten über <see cref="ViewData"/>-Schlüssel
/// (Titel, Beschreibung, Bild, Typ) und rufen danach
/// <see cref="RenderHeadAsync"/> auf. Fehlende Angaben werden sinnvoll
/// vorgegeben, damit jede indexierbare Seite eine Beschreibung hat.</para>
/// </summary>
public static class SeoMeta
{
    /// <summary>ViewData-Schlüssel für die Seitenbeschreibung.</summary>
    public const string DescriptionKey = "SeoDescription";

    /// <summary>ViewData-Schlüssel für das Vorschaubild (absolute URL).</summary>
    public const string ImageKey = "SeoImage";

    /// <summary>ViewData-Schlüssel für den Typ (article, website …).</summary>
    public const string TypeKey = "SeoType";

    /// <summary>ViewData-Schlüssel für den Veröffentlichungszeitpunkt (ISO-8601).</summary>
    public const string PublishedKey = "SeoPublished";

    /// <summary>Standardbeschreibung, wenn eine Seite keine eigene setzt.</summary>
    public const string DefaultDescription =
        "Mentalversagen — unabhängige Recherche zu Verschwörungen, Geheimnissen und " +
        "verdeckten Hintergründen. Aktenstücke, Belege und offene Fragen.";

    /// <summary>Standardtitel für die Startseite.</summary>
    public const string DefaultTitle = "Mentalversagen — Verschwörungen & Geheimnisse";

    /// <summary>Name der Website für Open Graph / strukturierte Daten.</summary>
    public const string SiteName = "Mentalversagen";

    /// <summary>
    /// Rendert die SEO-Metadaten: description, canonical, Open Graph, Twitter Card
    /// und (optional) JSON-LD. Gibt leeren Inhalt zurück, wenn nichts zu tun ist.
    /// </summary>
    /// <param name="viewData">Die ViewData der Seite.</param>
    /// <param name="context">Der HttpContext für Basis-URL und Pfad.</param>
    /// <param name="title">Der Seitentitel (ohne Seitenname-Suffix).</param>
    /// <param name="siteUrl">Der öffentliche Ursprung, z.B. "https://mentalversagen.de".</param>
    public static IHtmlContent RenderHead(
        IDictionary<string, object?> viewData,
        HttpContext context,
        string? title,
        string siteUrl)
    {
        var titleText = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();

        var description = GetString(viewData, DescriptionKey);
        if (string.IsNullOrWhiteSpace(description))
        {
            description = DefaultDescription;
        }

        description = Truncate(description.Trim(), 300);

        var image = GetString(viewData, ImageKey);
        var type = GetString(viewData, TypeKey) ?? "website";
        var published = GetString(viewData, PublishedKey);

        // Canonical: aktueller Pfad ohne Query-String (die Sitemap nutzt
        // ?slug=..., daher wird die Query beim Canonical bewusst mitgenommen,
        // wenn sie als "slug" vorliegt).
        var request = context.Request;
        var canonicalPath = request.Path.Value ?? "/";
        if (request.Query.TryGetValue("slug", out var slug) && !string.IsNullOrWhiteSpace(slug))
        {
            canonicalPath = $"{canonicalPath}?slug={Uri.EscapeDataString(slug.ToString())}";
        }

        var canonical = $"{siteUrl}{canonicalPath}";

        var sb = new StringBuilder();

        sb.Append("<meta name=\"description\" content=\"").Append(HtmlEncoder.Default.Encode(description)).AppendLine("\">");
        sb.Append("<link rel=\"canonical\" href=\"").Append(HtmlEncoder.Default.Encode(canonical)).AppendLine("\">");

        // Open Graph
        sb.Append("<meta property=\"og:site_name\" content=\"").Append(SiteName).AppendLine("\">");
        sb.Append("<meta property=\"og:type\" content=\"").Append(HtmlEncoder.Default.Encode(type)).AppendLine("\">");
        sb.Append("<meta property=\"og:title\" content=\"").Append(HtmlEncoder.Default.Encode(titleText)).AppendLine("\">");
        sb.Append("<meta property=\"og:description\" content=\"").Append(HtmlEncoder.Default.Encode(description)).AppendLine("\">");
        sb.Append("<meta property=\"og:url\" content=\"").Append(HtmlEncoder.Default.Encode(canonical)).AppendLine("\">");
        sb.Append("<meta property=\"og:locale\" content=\"de_DE\">").AppendLine();
        if (!string.IsNullOrWhiteSpace(image))
        {
            sb.Append("<meta property=\"og:image\" content=\"").Append(HtmlEncoder.Default.Encode(image)).AppendLine("\">");
        }

        // Twitter Card
        sb.Append("<meta name=\"twitter:card\" content=\"").Append(
            string.IsNullOrWhiteSpace(image) ? "summary" : "summary_large_image").AppendLine("\">");
        sb.Append("<meta name=\"twitter:title\" content=\"").Append(HtmlEncoder.Default.Encode(titleText)).AppendLine("\">");
        sb.Append("<meta name=\"twitter:description\" content=\"").Append(HtmlEncoder.Default.Encode(description)).AppendLine("\">");
        if (!string.IsNullOrWhiteSpace(image))
        {
            sb.Append("<meta name=\"twitter:image\" content=\"").Append(HtmlEncoder.Default.Encode(image)).AppendLine("\">");
        }

        // JSON-LD: WebSite für die Startseite, Article für Beiträge.
        var jsonLd = BuildJsonLd(viewData, titleText, description, canonical, image, published, siteUrl);
        if (!string.IsNullOrWhiteSpace(jsonLd))
        {
            sb.Append("<script type=\"application/ld+json\">")
              .Append(jsonLd)
              .AppendLine("</script>");
        }

        return new HtmlString(sb.ToString());
    }

    private static string? BuildJsonLd(
        IDictionary<string, object?> viewData,
        string title,
        string description,
        string canonical,
        string? image,
        string? published,
        string siteUrl)
    {
        var type = GetString(viewData, TypeKey) ?? "website";

        var payload = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["name"] = title,
            ["description"] = description,
            ["url"] = canonical,
        };

        if (type == "article")
        {
            payload["@type"] = "Article";
            payload["headline"] = title;
            payload["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = SiteName,
                ["url"] = siteUrl
            };
            if (!string.IsNullOrWhiteSpace(image))
            {
                payload["image"] = image;
            }

            if (!string.IsNullOrWhiteSpace(published)
                && DateTimeOffset.TryParse(published, out var publishedAt))
            {
                payload["datePublished"] = publishedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
        }
        else
        {
            payload["@type"] = "WebSite";
            payload["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = new Dictionary<string, object?>
                {
                    ["@type"] = "EntryPoint",
                    ["urlTemplate"] = $"{siteUrl}/Suche?q={{search_term_string}}"
                },
                ["query-input"] = "required name=search_term_string"
            };
        }

        // WICHTIG: Das JSON wird in ein <script type="application/ld+json"> geschrieben.
        // Der Standard-JSON-Serializer escaped zwar Anfuehrungszeichen, aber nicht
        // "<" und ">" — damit koennte eine Description wie "</script><script>…"
        // das Script-Element vorzeitig schliessen (XSS). Deshalb wird hier
        // zusaetzlich auf "<", ">" und "&" escaped.
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return json
            .Replace("<", "\\u003C", StringComparison.Ordinal)
            .Replace(">", "\\u003E", StringComparison.Ordinal)
            .Replace("&", "\\u0026", StringComparison.Ordinal);
    }

    private static string? GetString(IDictionary<string, object?> viewData, string key)
    {
        return viewData.TryGetValue(key, out var value) ? value as string : null;
    }

    /// <summary>Kürzt einen Text auf eine Höchstlänge (mit Auslassungspunkten).</summary>
    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        var cut = text[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd() + "…";
    }
}
