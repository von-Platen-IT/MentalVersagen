using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Content;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Regressionstests für die Hashtag-Normalisierung (Slug-basierte Eindeutigkeit,
/// Case-Insensitive-Suche, keine Duplikate in der UI-Auswahl).
///
/// Hintergrund: "Wählen" / "waehlen" / "WAEHLEN" sind Schreibweisen desselben
/// Hashtags (SlugGenerator bildet ä→ae und lowercased). Sie müssen zu EINEM
/// Hashtag zusammengeführt werden, und die Suche über den Slug muss in jeder
/// Schreibweise denselben Artikel finden.
/// </summary>
public class HashtagNormalizationTests
{
    private static async Task<ArticleService> SeedArticleWithHashtagsAsync(
        params string[] hashtagNames)
    {
        var db = TestDb.Create();
        var slugGen = new SlugGenerator();

        var author = new User
        {
            Id = Guid.NewGuid(),
            UserName = "autor@example.de",
            Email = "autor@example.de",
            DisplayName = "Autor",
            Role = UserRole.Author
        };
        db.Users.Add(author);

        var article = new Article
        {
            Id = Guid.NewGuid(),
            Title = "Diagnose-Artikel",
            Slug = "diagnose-artikel",
            Excerpt = "Kurz",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Politik,
            AccessLevel = ArticleAccessLevel.Public,
            Status = ArticleStatus.Published,
            PublishedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            AuthorId = author.Id
        };
        db.Articles.Add(article);

        var service = new ArticleService(db, slugGen);
        await service.PrepareCreateAsync(article, [], hashtagNames);
        await db.SaveChangesAsync();

        return service;
    }

    /// <summary>
    /// Umlaut- und Transliterationsschreibweisen fließen auf einen Hashtag zusammen.
    /// </summary>
    [Fact]
    public async Task UmlautUndTransliteration_FliessenAufGleichenHashtag()
    {
        var service = await SeedArticleWithHashtagsAsync("Wählen", "waehlen");

        var all = await service.GetAllHashtagsAsync();

        // Erwartet: genau EIN Hashtag mit Slug "waehlen".
        Assert.True(
            all.Count == 1,
            $"Erwartet genau 1 Hashtag (Slug='waehlen'), aber gefunden: " +
            string.Join(", ", all.Select(h => $"{h.Name}/{h.Slug}")));
    }

    /// <summary>
    /// Die Hashtag-Suche über den Slug ist case-insensitiv — ein Klick auf den
    /// Hashtag-Link muss den Artikel finden, egal wie der Slug geschrieben ist.
    /// </summary>
    [Theory]
    [InlineData("waehlen")]
    [InlineData("WAEHLEN")]
    [InlineData("Waehlen")]
    [InlineData("WÄHLEN")]
    [InlineData("#waehlen")]
    public async Task HashtagSuche_IstCaseInsensitive(string searchTerm)
    {
        var service = await SeedArticleWithHashtagsAsync("Wählen");

        var (items, total) = await service.SearchPublishedAsync(
            new ArticleQuery(1, 10, HashtagSlug: searchTerm));

        Assert.True(
            total == 1,
            $"Hashtag-Suche nach '{searchTerm}' liefert {total} Treffer, erwartet 1.");
    }

    /// <summary>
    /// GetPublishedHashtagsAsync liefert keinen Hashtag doppelt, auch wenn
    /// verschiedene Schreibweisen (Name-Case, Umlaut-Transliteration) vorliegen.
    /// </summary>
    [Fact]
    public async Task PublishedHashtags_KeineDoppelungen()
    {
        var service = await SeedArticleWithHashtagsAsync("Wählen", "waehlen", "WAEHLEN");

        var usage = await service.GetPublishedHashtagsAsync();

        var slugs = usage.Select(u => u.Slug).ToList();
        Assert.True(
            slugs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == slugs.Count,
            $"Hashtag-Slugs enthalten Duplikate: {string.Join(", ", slugs)}");
    }

    /// <summary>
    /// Der Sync darf nie zwei Datensätze mit gleichem Slug (case-insensitive)
    /// erzeugen — PostgreSQL kollationiert varchar case-sensitive, daher muss der
    /// Slug bereits normalisiert lowercase vorliegen und der Lookup case-insensitive
    /// erfolgen.
    /// </summary>
    [Fact]
    public async Task SyncHashtags_ErzeugtKeineGleichenSlugs()
    {
        var service = await SeedArticleWithHashtagsAsync("Politik", "politik", "POLITIK");

        var all = await service.GetAllHashtagsAsync();
        var slugs = all.Select(h => h.Slug.ToLowerInvariant()).ToList();

        Assert.True(
            slugs.Distinct().Count() == slugs.Count,
            $"Slugs nach Lowercase nicht eindeutig: {string.Join(", ", slugs)}");
    }

    /// <summary>
    /// Zusätzlich zu den Hashtags gilt dasselbe für Tags: "Müller" / "mueller"
    /// müssen zu einem Tag zusammengeführt werden.
    /// </summary>
    [Fact]
    public async Task Tags_UmlautUndTransliteration_FliessenAufGleichenTag()
    {
        var db = TestDb.Create();
        var slugGen = new SlugGenerator();

        var author = new User
        {
            Id = Guid.NewGuid(),
            UserName = "autor2@example.de",
            Email = "autor2@example.de",
            DisplayName = "Autor",
            Role = UserRole.Author
        };
        db.Users.Add(author);

        var article = new Article
        {
            Id = Guid.NewGuid(),
            Title = "Tag-Diagnose",
            Slug = "tag-diagnose",
            Excerpt = "Kurz",
            ContentMarkdown = "Inhalt",
            Category = ArticleCategory.Politik,
            AccessLevel = ArticleAccessLevel.Public,
            Status = ArticleStatus.Published,
            PublishedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            AuthorId = author.Id
        };
        db.Articles.Add(article);

        var service = new ArticleService(db, slugGen);
        await service.PrepareCreateAsync(article, ["Müller", "mueller"], []);
        await db.SaveChangesAsync();

        var all = await service.GetAllTagsAsync();

        Assert.True(
            all.Count == 1,
            $"Erwartet genau 1 Tag (Slug='mueller'), aber gefunden: " +
            string.Join(", ", all.Select(t => $"{t.Name}/{t.Slug}")));
    }
}
