using System.Text;
using System.Xml.Linq;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Web.Seo;

/// <summary>
/// Erzeugt die Sitemap für die öffentlich sichtbaren Inhalte.
///
/// <para>Öffentlich ist alles, was ohne Anmeldung erreichbar ist: die
/// statischen Seiten, veröffentlichte Artikel und veröffentlichte Linklisten.
/// Entwürfe, geplante und archivierte Artikel sowie nicht veröffentlichte
/// Linklisten gehören NICHT in die Sitemap — sie sollen nicht indexiert werden.</para>
///
/// <para>Die Sitemap enthält ausschliesslich absolute URLs mit dem
/// konfigurierten öffentlichen Ursprung (<see cref="SiteOptions"/>).</para>
/// </summary>
public class SitemapBuilder(AppDbContext db, IHttpContextAccessor accessor)
{
    /// <summary>Öffentliche, statische Seiten (feste Pfade).</summary>
    private static readonly (string Path, string Priority, string ChangeFreq)[] StaticPages =
    [
        ("/", "1.0", "daily"),
        ("/Suche", "0.6", "weekly"),
        ("/Kontakt", "0.5", "monthly"),
        ("/Datenschutz", "0.3", "yearly"),
        ("/Impressum", "0.3", "yearly"),
        ("/Newsletter/Subscribe", "0.5", "monthly")
    ];

    /// <summary>
    /// Baut das Sitemap-XML. <paramref name="baseUrl"/> ist der öffentliche
    /// Ursprung ohne Trailing-Slash, z.B. "https://mentalversagen.de".
    /// </summary>
    public async Task<string> BuildAsync(string baseUrl, CancellationToken ct = default)
    {
        var urls = new List<(string Loc, string? LastMod, string Priority, string ChangeFreq)>();

        foreach (var (path, priority, changeFreq) in StaticPages)
        {
            urls.Add(($"{baseUrl}{path}", null, priority, changeFreq));
        }

        // Veröffentlichte Artikel
        var articles = await db.Articles
            .AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published)
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync(ct);

        foreach (var article in articles)
        {
            var loc = $"{baseUrl}/Articles/Details?slug={Uri.EscapeDataString(article.Slug)}";
            var lastMod = article.PublishedAt?.ToUniversalTime().ToString("yyyy-MM-dd");
            urls.Add((loc, lastMod, "0.8", "weekly"));
        }

        // Linklisten: es gibt kein Sichtbarkeits-Feld, jede Liste ist öffentlich
        // erreichbar. Ihre Artikel werden gefiltert, die Liste selbst nicht.
        var linkLists = await db.LinkLists
            .AsNoTracking()
            .OrderBy(l => l.Title)
            .ToListAsync(ct);

        foreach (var list in linkLists)
        {
            var loc = $"{baseUrl}/LinkLists/{Uri.EscapeDataString(list.Slug)}";
            var lastMod = list.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd");
            urls.Add((loc, lastMod, "0.7", "monthly"));
        }

        var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
        var root = new XElement(ns + "urlset");
        foreach (var (loc, lastMod, priority, changeFreq) in urls)
        {
            var url = new XElement(ns + "url",
                new XElement(ns + "loc", loc));
            if (!string.IsNullOrEmpty(lastMod))
            {
                url.Add(new XElement(ns + "lastmod", lastMod));
            }

            url.Add(new XElement(ns + "priority", priority));
            url.Add(new XElement(ns + "changefreq", changeFreq));
            root.Add(url);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);

        // XDocument.WriteTo schreibt die Deklaration als utf-16, wenn ueber einen
        // StringWriter serialisiert wird. Deshalb wird das XML direkt mit
        // XmlWriter in einen MemoryStream geschrieben - dort stimmt die
        // Byte-Deklaration (utf-8) mit den tatsaechlichen Bytes ueberein.
        using var stream = new MemoryStream();
        var settings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };

        using (var writer = System.Xml.XmlWriter.Create(stream, settings))
        {
            doc.Save(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
