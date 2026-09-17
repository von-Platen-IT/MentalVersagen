namespace BlogCms.Web.Configuration;

/// <summary>
/// Minimalistischer Loader für <c>.env</c>-Dateien (Format <c>KEY=VALUE</c>) ohne
/// externe Abhängigkeit.
///
/// Die Werte werden als Prozess-Umgebungsvariablen gesetzt, damit die
/// Standard-<c>EnvironmentVariables</c>-Konfigurationsquelle sie aufnimmt. Über
/// die .NET-Konvention <c>__</c> als Trenner mappt bspw.
/// <c>Features__MonetizationEnabled</c> auf <c>Features:MonetizationEnabled</c>.
///
/// Bereits existierende Umgebungsvariablen werden nicht überschrieben: in Docker
/// gesetzte Werte haben somit Vorrang vor der Datei.
/// </summary>
public static class EnvFileLoader
{
    /// <summary>
    /// Sucht die angegebene Datei vom aktuellen Arbeitsverzeichnis aufwärts und
    /// lädt sie, sofern vorhanden. Fehlt die Datei, passiert nichts.
    /// </summary>
    public static void Load(string fileName = ".env")
    {
        var path = FindEnvFile(fileName);
        if (path is null)
        {
            return;
        }

        foreach (var (key, value) in Parse(File.ReadLines(path)))
        {
            // Reale Umgebungsvariablen (z. B. aus Docker) haben Vorrang.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    /// <summary>
    /// Parst <c>.env</c>-Zeilen zu Schlüssel/Wert-Paaren. Unterstützt Kommentare
    /// (<c>#</c>), Leerzeilen, ein optionales <c>export </c>-Präfix, umschließende
    /// einfache/doppelte Anführungszeichen sowie <c>=</c> im Wert. Bei mehrfach
    /// vorkommenden Schlüsseln gewinnt der letzte Eintrag.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Kommentare und Leerzeilen ignorieren.
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            // Optionales "export " (POSIX-Stil) entfernen.
            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = Unquote(line[(separatorIndex + 1)..].Trim());
        }

        return result;
    }

    private static string? FindEnvFile(string fileName)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
