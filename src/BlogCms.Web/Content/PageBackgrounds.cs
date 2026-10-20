using System.Reflection;

namespace BlogCms.Web.Content;

/// <summary>
/// Hintergrundmotive des Seiten-Headers (<c>_PageHeader.cshtml</c>).
///
/// Der Header zeigt bewusst nicht immer dasselbe Motiv: Pro Seitenaufruf wird
/// zufällig eines aus dem Pool gewählt, sodass jeder Aufruf ein anderes
/// Bild zeigt und die Seite wie ein wechselndes Plakat wirkt. Die Auswahl
/// erfolgt serverseitig, damit das Motiv auch ohne JavaScript (und ohne
/// Nachladen) im HTML steht.
///
/// <para><b>Pool:</b> die drei klassischen Header-Slides plus alle WebP-Motive
/// aus <c>wwwroot/pics/</c>. Neue Bilder werden zur Laufzeit automatisch
/// aufgenommen — sie müssen nur als WebP in <c>wwwroot/pics/</c> liegen.
/// Die statische Liste unten dient als Rückfallebene (z. B. in Tests, wenn das
/// Verzeichnis nicht gefunden wird) und als Dokumentation des Bestands.</para>
/// </summary>
public static class PageBackgrounds
{
    /// <summary>Die klassischen Header-Slides (feste Motive).</summary>
    private static readonly string[] Slides =
    [
        "/pics/header_slides_1.png",
        "/pics/header_slides_2.png",
        "/pics/header_slides_3.png"
    ];

    /// <summary>
    /// Statischer Bestand der WebP-Motive (Rückfallebene und Übersicht).
    /// </summary>
    private static readonly string[] KnownMotifs =
    [
        "/pics/01_S1_1_r07_c08_bg.webp",
        "/pics/02_S1_1_r02_c05_bg.webp",
        "/pics/03_S1_1_r03_c03_bg.webp",
        "/pics/04_S1_1_r04_c01_bg.webp",
        "/pics/05_S1_1_r07_c09_bg.webp",
        "/pics/06_S2_2_r03_c01_bg.webp",
        "/pics/07_S2_2_r03_c02_bg.webp",
        "/pics/08_S2_2_r04_c02_bg.webp",
        "/pics/13_S1_1_r02_c04_bg.webp",
        "/pics/15_S1_1_r03_c05_bg.webp",
        "/pics/18_S1_1_r05_c08_bg.webp",
        "/pics/20_S1_1_r05_c11_bg.webp",
        "/pics/antarktika_weltauge.webp",
        "/pics/antikythera_maschine.webp",
        "/pics/area51_roswell.webp",
        "/pics/atlantis_zeitreisende.webp",
        "/pics/bernsteinkammer.webp",
        "/pics/bimini_weg.webp",
        "/pics/flug19_bermuda.webp",
        "/pics/mk_ultra_vietnam.webp",
        "/pics/mount_weather.webp",
        "/pics/philadelphia_experiment.webp",
        "/pics/pyramiden_ausserirdische.webp",
        "/pics/raketenkrise_schweinebucht.webp",
        "/pics/thonis_heracleion.webp",
        "/pics/tschernobyl.webp",
        "/pics/vril_gesellschaft.webp"
    ];

    private static readonly Lazy<IReadOnlyList<string>> MotifPool = new(BuildPool);

    /// <summary>Alle verfügbaren Motive (Pfad relativ zur Website-Root).</summary>
    public static IReadOnlyList<string> All => MotifPool.Value;

    /// <summary>
    /// Liefert ein zufällig gewähltes Motiv. Die Verteilung ist gleichverteilt;
    /// aufeinanderfolgende Aufrufe können dasselbe Motiv liefern — das ist
    /// gewollt und vermeidet den Eindruck einer Rotation.
    /// </summary>
    public static string Next() => All[Random.Shared.Next(All.Count)];

    /// <summary>
    /// Liefert das Motiv für eine feste Slide-Nummer (1–3) oder — wenn keine
    /// Slide vorgegeben ist (0) — ein zufälliges Motiv.
    /// </summary>
    public static string ForSlide(int slide) => slide is >= 1 and <= 3
        ? Slides[slide - 1]
        : Next();

    /// <summary>
    /// Baut den Motiv-Pool: klassische Slides + statischer Bestand + alle WebP-
    /// Dateien, die im Bildverzeichnis der Anwendung liegen. Dadurch fließen
    /// neu hinzugefügte Motive automatisch in die Auswahl ein.
    /// </summary>
    private static IReadOnlyList<string> BuildPool()
    {
        var pool = new List<string>(Slides.Length + KnownMotifs.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var motif in Slides.Concat(KnownMotifs))
        {
            if (seen.Add(motif))
            {
                pool.Add(motif);
            }
        }

        foreach (var file in EnumerateMotifFiles())
        {
            var path = "/pics/" + Path.GetFileName(file);
            if (seen.Add(path))
            {
                pool.Add(path);
            }
        }

        return pool;
    }

    /// <summary>
    /// Sucht die WebP-Motive im Bildverzeichnis der Anwendung. Geprüft werden
    /// das Publish-Output, der Projektordner (Development) und ein optionales
    /// Override über die Umgebungsvariable <c>MV_PICS_DIR</c>.
    /// </summary>
    private static IEnumerable<string> EnumerateMotifFiles()
    {
        var baseDirectory = AppContext.BaseDirectory;

        var candidates = new List<string>
        {
            // Veröffentlichtes Output: wwwroot liegt neben der DLL.
            Path.Combine(baseDirectory, "wwwroot", "pics"),
            // Entwicklung: bin/<config>/<tfm> → 3 Ebenen hoch liegt das Projekt.
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "wwwroot", "pics"))
        };

        var overrideDirectory = Environment.GetEnvironmentVariable("MV_PICS_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            candidates.Insert(0, overrideDirectory);
        }

        // Der Ordner der Web-Anwendung gezielt suchen (falls die Anwendung in
        // einem Test- oder Tool-Kontext läuft).
        var assemblyDirectory = Path.GetDirectoryName(typeof(PageBackgrounds).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDirectory))
        {
            candidates.Add(Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "wwwroot", "pics")));
        }

        var seenDirectories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directory in candidates)
        {
            if (!seenDirectories.Add(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.webp");
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }
}
