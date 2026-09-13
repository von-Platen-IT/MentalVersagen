using System.Text;
using System.Xml.Linq;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Media;
using BlogCms.Web.Content;
using Microsoft.AspNetCore.Mvc;

namespace BlogCms.Web.Controllers;

/// <summary>
/// RSS 2.0 feed over all published articles (see 01-Content-Verwaltung.md).
/// </summary>
[Route("feed")]
public class FeedController : Controller
{
    private const int MaxItems = 50;

    private readonly IArticleService _articles;
    private readonly IMediaService _media;

    public FeedController(IArticleService articles, IMediaService media)
    {
        _articles = articles;
        _media = media;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (items, _) = await _articles.GetPublishedAsync(1, MaxItems, cancellationToken: cancellationToken);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        XNamespace atom = "http://www.w3.org/2005/Atom";

        var channel = new XElement("channel",
            new XElement("title", "MentalVersagen"),
            new XElement("link", baseUrl),
            new XElement("description", "Politik, Satire und Verschwörungstheorien"),
            new XElement(atom + "link",
                new XAttribute("href", $"{baseUrl}/feed"),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/rss+xml")),
            items.Select(a =>
            {
                var titleImage = ArticleDisplay.ResolveTitleImage(a, _media);
                var item = new XElement("item",
                    new XElement("title", a.Title),
                    new XElement("link", $"{baseUrl}/Articles/{a.Slug}"),
                    new XElement("guid", $"{baseUrl}/Articles/{a.Slug}"),
                    new XElement("category", a.Category.ToString()),
                    new XElement("pubDate", (a.PublishedAt ?? a.CreatedAt).ToUniversalTime().ToString("R")),
                    new XElement("description", a.Excerpt ?? string.Empty));

                // Title image as an enclosure so RSS readers can show it (BR-100/BR-022).
                if (!string.IsNullOrWhiteSpace(titleImage))
                {
                    item.Add(new XElement("enclosure",
                        new XAttribute("url", titleImage),
                        new XAttribute("type", "image/jpeg"),
                        new XAttribute("length", "0")));
                }

                return item;
            }));

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "atom", atom), channel));

        // UTF-8 declaration is required by RSS 2.0 validators.
        var xml = document.Declaration + Environment.NewLine + document.ToString();
        return Content(xml, "application/rss+xml", Encoding.UTF8);
    }
}
