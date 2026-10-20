using BlogCms.Web.Content;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der Header-Hintergrundmotive: Pro Seitenaufruf wird zufällig eines aus
/// dem Pool gewählt, eine feste Slide (1–3) liefert weiterhin die klassische
/// Header-Slide. Neue Motive werden automatisch aus <c>wwwroot/pics/</c>
/// aufgenommen.
/// </summary>
public class PageBackgroundsTests
{
    [Fact]
    public void All_ContainsTheThreeClassicSlides()
    {
        var all = PageBackgrounds.All;

        Assert.Contains("/pics/header_slides_1.png", all);
        Assert.Contains("/pics/header_slides_2.png", all);
        Assert.Contains("/pics/header_slides_3.png", all);
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        Assert.Equal(PageBackgrounds.All.Count, PageBackgrounds.All.Distinct().Count());
    }

    [Fact]
    public void All_ContainsAtLeastTheKnownMotifs()
    {
        // Drei Slides + die 27 im Code hinterlegten Motive.
        Assert.True(PageBackgrounds.All.Count >= 30,
            $"Der Motiv-Pool enthält nur {PageBackgrounds.All.Count} Motive.");
    }

    [Fact]
    public void All_NonSlideMotifsUseWebp()
    {
        var nonSlides = PageBackgrounds.All
            .Where(p => !p.Contains("header_slides", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(nonSlides);
        Assert.All(nonSlides, p => Assert.EndsWith(".webp", p, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, "/pics/header_slides_1.png")]
    [InlineData(2, "/pics/header_slides_2.png")]
    [InlineData(3, "/pics/header_slides_3.png")]
    public void ForSlide_FixedSlideReturnsSlide(int slide, string expected)
    {
        Assert.Equal(expected, PageBackgrounds.ForSlide(slide));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void ForSlide_WithoutValidSlideReturnsRandomMotif(int slide)
    {
        Assert.Contains(PageBackgrounds.ForSlide(slide), PageBackgrounds.All);
    }

    [Fact]
    public void Next_ReturnsMotifFromPool()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(PageBackgrounds.Next(), PageBackgrounds.All);
        }
    }

    [Fact]
    public void Next_IsRandomizedAcrossCalls()
    {
        // 50 Ziehungen: mit hoher Wahrscheinlichkeit sind mehrere Motive dabei.
        var seen = Enumerable.Range(0, 50).Select(_ => PageBackgrounds.Next()).Distinct().Count();

        Assert.True(seen > 1, "Die Motiv-Auswahl liefert bei 50 Ziehungen nur ein Motiv.");
    }

    [Fact]
    public void All_ContainsEveryWebpMotifFoundInTheWebProject()
    {
        var directory = FindWebPicsDirectory();
        if (directory is null)
        {
            // Bildverzeichnis nicht auffindbar (z. B. isolierter Testlauf) —
            // die statische Rückfallebene genügt dann.
            return;
        }

        var expected = Directory
            .EnumerateFiles(directory, "*.webp")
            .Select(f => "/pics/" + Path.GetFileName(f))
            .ToList();

        Assert.NotEmpty(expected);
        foreach (var motif in expected)
        {
            Assert.Contains(motif, PageBackgrounds.All);
        }
    }

    /// <summary>
    /// Sucht das Bildverzeichnis des Web-Projekts (aufsteigend ab dem
    /// Test-Ausgabeordner).
    /// </summary>
    private static string? FindWebPicsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BlogCms.Web", "wwwroot", "pics");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
