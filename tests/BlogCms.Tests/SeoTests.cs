using System.Xml.Linq;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Web.Seo;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der SEO-Grundlage: Sitemap, robots.txt und die Meta-Tags im Head.
///
/// <para>Geprüft wird vor allem das, was bei Suchmaschinen stillschweigend
/// falsch sein kann: nicht indexierbare Inhalte in der Sitemap, ein
/// falsches Encoding der XML-Deklaration, fehlende Beschreibungen und
/// absolute URLs mit dem internen Host hinter dem Reverse Proxy.</para>
/// </summary>
public class SeoTests
{
    private const string BaseUrl = "https://mentalversagen.de";

    private static HttpContext CreateContext(string path = "/", string? query = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("mentalversagen.de");
        context.Request.Path = path;
        if (query is not null)
        {
            context.Request.QueryString = new QueryString(query);
        }

        return context;
    }

    private static SitemapBuilder CreateBuilder(params Article[] articles)
    {
        var db = TestDb.Create();
        if (articles.Length > 0)
        {
            db.Articles.AddRange(articles);
            db.SaveChanges();
        }

        return new SitemapBuilder(db, new HttpContextAccessor { HttpContext = CreateContext() });
    }

    // ------------------------------------------------------------------
    // Sitemap
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sitemap_ContainsTheStaticPages()
    {
        var builder = CreateBuilder();
        var xml = await builder.BuildAsync(BaseUrl);

        Assert.Contains($"{BaseUrl}/", xml);
        Assert.Contains($"{BaseUrl}/Kontakt", xml);
        Assert.Contains($"{BaseUrl}/Datenschutz", xml);
        Assert.Contains($"{BaseUrl}/Impressum", xml);
    }

    [Fact]
    public async Task Sitemap_ContainsPublishedArticles()
    {
        var builder = CreateBuilder(
            new Article { Title = "Akte A", Slug = "akte-a", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow });

        var xml = await builder.BuildAsync(BaseUrl);

        Assert.Contains("slug=akte-a", xml);
    }

    [Fact]
    public async Task Sitemap_ExcludesUnpublishedArticles()
    {
        var builder = CreateBuilder(
            new Article { Title = "Entwurf", Slug = "entwurf", Status = ArticleStatus.Draft },
            new Article { Title = "Geplant", Slug = "geplant", Status = ArticleStatus.Scheduled },
            new Article { Title = "Archiviert", Slug = "archiviert", Status = ArticleStatus.Archived });

        var xml = await builder.BuildAsync(BaseUrl);

        Assert.DoesNotContain("entwurf", xml);
        Assert.DoesNotContain("geplant", xml);
        Assert.DoesNotContain("archiviert", xml);
    }

    [Fact]
    public async Task Sitemap_UsesTheGivenBaseUrlAndNotTheInternalHost()
    {
        var builder = CreateBuilder();
        var xml = await builder.BuildAsync(BaseUrl);

        Assert.Contains(BaseUrl, xml);
        Assert.DoesNotContain("127.0.0.1", xml);
        Assert.DoesNotContain("blogcms-app", xml);
    }

    [Fact]
    public async Task Sitemap_DoesNotContainPrivateAreas()
    {
        var builder = CreateBuilder();
        var xml = await builder.BuildAsync(BaseUrl);

        Assert.DoesNotContain("/Admin", xml);
        Assert.DoesNotContain("/Account", xml);
        Assert.DoesNotContain("/Membership", xml);
    }

    [Fact]
    public async Task Sitemap_DeclaresUtf8AndNotUtf16()
    {
        var builder = CreateBuilder();
        var xml = await builder.BuildAsync(BaseUrl);

        // Regression: XDocument.WriteTo schreibt ueber einen StringWriter
        // "encoding=utf-16" in die Deklaration, obwohl UTF-8-Bytes gesendet werden.
        Assert.Contains("encoding=\"utf-8\"", xml);
        Assert.DoesNotContain("utf-16", xml);
    }

    [Fact]
    public async Task Sitemap_IsWellFormedXml()
    {
        var builder = CreateBuilder(
            new Article { Title = "Akte", Slug = "akte", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow });

        var xml = await builder.BuildAsync(BaseUrl);

        var doc = XDocument.Parse(xml);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = doc.Root!.Elements(ns + "url").ToList();

        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.StartsWith(BaseUrl, u.Element(ns + "loc")!.Value));
    }

    [Fact]
    public async Task Sitemap_EscapesSlugsWithSpecialCharacters()
    {
        var builder = CreateBuilder(
            new Article { Title = "Ä & Co", Slug = "a-und-co", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow });

        var xml = await builder.BuildAsync(BaseUrl);

        Assert.Contains("slug=a-und-co", xml);
    }

    // ------------------------------------------------------------------
    // Meta-Tags
    // ------------------------------------------------------------------

    [Fact]
    public void RenderHead_EmitsDescriptionCanonicalAndOpenGraph()
    {
        var viewData = new Dictionary<string, object?>
        {
            [SeoMeta.DescriptionKey] = "Beschreibung der Seite.",
            [SeoMeta.ImageKey] = $"{BaseUrl}/pics/beispiel.webp",
            [SeoMeta.TypeKey] = "website"
        };

        var html = SeoMeta.RenderHead(viewData, CreateContext(), "Titel", BaseUrl).ToString()!;

        Assert.Contains("<meta name=\"description\"", html);
        Assert.Contains("Beschreibung der Seite.", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://mentalversagen.de/\"", html);
        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("property=\"og:url\"", html);
        Assert.Contains("property=\"og:image\"", html);
        Assert.Contains("name=\"twitter:card\"", html);
    }

    [Fact]
    public void RenderHead_FallsBackToDefaultDescription()
    {
        var html = SeoMeta.RenderHead(
            new Dictionary<string, object?>(), CreateContext(), "Titel", BaseUrl).ToString()!;

        Assert.Contains("<meta name=\"description\"", html);
        Assert.Contains("Verschwörungen", html);
    }

    [Fact]
    public void RenderHead_KeepsTheSlugInTheCanonicalUrl()
    {
        var context = CreateContext("/Articles/Details", "?slug=akte-a");
        var html = SeoMeta.RenderHead(
            new Dictionary<string, object?>(), context, "Titel", BaseUrl).ToString()!;

        Assert.Contains("canonical\" href=\"https://mentalversagen.de/Articles/Details?slug=akte-a\"", html);
    }

    [Fact]
    public void RenderHead_EmitsArticleJsonLdWithPublishedDate()
    {
        var viewData = new Dictionary<string, object?>
        {
            [SeoMeta.TypeKey] = "article",
            [SeoMeta.PublishedKey] = "2026-10-20T12:00:00Z"
        };

        var html = SeoMeta.RenderHead(viewData, CreateContext(), "Ein Aktenstück", BaseUrl).ToString()!;

        Assert.Contains("\"@type\":\"Article\"", html);
        Assert.Contains("datePublished", html);
        Assert.Contains("2026-10-20", html);
    }

    [Fact]
    public void RenderHead_EmitsWebSiteJsonLdWithSearchAction()
    {
        var html = SeoMeta.RenderHead(
            new Dictionary<string, object?>(), CreateContext(), "Start", BaseUrl).ToString()!;

        Assert.Contains("\"@type\":\"WebSite\"", html);
        Assert.Contains("SearchAction", html);
    }

    [Fact]
    public void RenderHead_EscapesHtmlInTheDescription()
    {
        var viewData = new Dictionary<string, object?>
        {
            [SeoMeta.DescriptionKey] = "Text mit <script>alert(1)</script>"
        };

        var html = SeoMeta.RenderHead(viewData, CreateContext(), "Titel", BaseUrl).ToString()!;

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void RenderHead_CannotBeUsedToBreakOutOfTheJsonLdScript()
    {
        // Regression: eine Description wie "</script><script>alert(1)</script>"
        // wuerde im JSON-LD-Block das Script-Element vorzeitig schliessen.
        var viewData = new Dictionary<string, object?>
        {
            [SeoMeta.DescriptionKey] = "</script><script>alert(1)</script>"
        };

        var html = SeoMeta.RenderHead(viewData, CreateContext(), "Titel", BaseUrl).ToString()!;

        // Der JSON-LD-Block darf kein vorzeitiges </script> enthalten.
        var jsonLdStart = html.IndexOf("application/ld+json", StringComparison.Ordinal);
        Assert.True(jsonLdStart > 0, "Kein JSON-LD-Block gefunden.");

        var jsonLd = html[jsonLdStart..];
        var end = jsonLd.IndexOf("</script>", StringComparison.Ordinal);
        var content = jsonLd[..end];

        Assert.DoesNotContain("</script", content);
        Assert.DoesNotContain("<script", content);
    }

    [Theory]
    [InlineData("kurz", 300, "kurz")]
    [InlineData("", 300, "")]
    public void Truncate_KeepsShortTextsUnchanged(string text, int max, string expected)
    {
        Assert.Equal(expected, SeoMeta.Truncate(text, max));
    }

    [Fact]
    public void Truncate_CutsLongTextsAndAddsEllipsis()
    {
        var text = new string('a', 400) + " ende";

        var result = SeoMeta.Truncate(text, 100);

        Assert.True(result.Length <= 101);
        Assert.EndsWith("…", result);
    }
}
