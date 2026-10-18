# Plan: ImageSharp durch Magick.NET ersetzen

**Stand:** 2026-10-18 — **umgesetzt** (siehe Abschnitt 7)
**Ziel:** Die kommerzialisierte Bibliothek `SixLabors.ImageSharp` (Six Labors Split License) vollständig durch eine freie, quelloffene Alternative ersetzen — ohne Verhaltensänderung für Nutzer und ohne Änderung an der Anwendungsschnittstelle.

---

## 1. Ausgangslage: Wo wird ImageSharp verwendet?

| Stelle | Verwendung |
|---|---|
| [`ImageProcessor.cs`](../src/BlogCms.Infrastructure/Media/ImageProcessor.cs:26) | **Einzige fachliche Nutzung:** Decode, Format-Erkennung, EXIF-AutoOrient, proportionale Größenanpassung, Re-Encode als WebP/PNG/GIF/JPEG, Fehlerbehandlung |
| [`Program.cs`](../src/BlogCms.Web/Program.cs:173) | DI-Registrierung `IImageProcessor → ImageSharpImageProcessor` |
| [`BlogCms.Infrastructure.csproj`](../src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj:24) | Paketreferenz `SixLabors.ImageSharp` 4.1.3 |
| Tests | **Keine direkte Nutzung** — [`MediaUploadLimitTests`](../tests/BlogCms.Tests/MediaUploadLimitTests.cs) und [`ArticlePostingTests`](../tests/BlogCms.Tests/ArticlePostingTests.cs) verwenden Fakes (`IImageProcessor`) |

Die Anwendungsschnittstelle [`IImageProcessor`](../src/BlogCms.Infrastructure/Media/ImageProcessor.cs:16) bleibt unverändert — der Austausch betrifft nur die Implementierung.

---

## 2. Alternativen-Bewertung (Context7-recherchiert)

| Kriterium | **Magick.NET** (Apache-2.0) | SkiaSharp (MIT) |
|---|---|---|
| Lizenz | Apache-2.0 — frei, auch kommerziell | MIT — frei |
| Decode (JPEG/PNG/WebP/GIF) | ✅ 100+ Formate | ✅ |
| EXIF-AutoOrient | ✅ `image.AutoOrient()` — ein Aufruf | ⚠️ manuell: `SKCodec.EncodedOrigin` lesen und 8 Fälle selbst transformieren |
| Proportionale Größenanpassung | ✅ `Resize(new MagickGeometry(w, h))` (fit inside) | ✅ `SKBitmap.Resize` |
| Encode WebP | ✅ `MagickFormat.WebP` | ✅ |
| Encode **GIF** | ✅ `MagickFormat.Gif` | ❌ Skia kann GIF nicht schreiben |
| Native Abhängigkeiten | NuGet bringt alles mit (keine System-Installation) | NuGet bringt native Libs mit |
| Aufwand für 1:1-Ersatz | **gering** | mittel (Orientierung + GIF-Sonderfall) |

**Entscheidung: `Magick.NET-Q8-AnyCPU`** (8-Bit-Variante — für Web-Bilder ausreichend, kleiner und schneller als Q16). Sie erlaubt einen nahezu 1:1-Ersatz aller bisherigen Operationen.

---

## 3. Operations-Mapping (alt → neu)

| Bisher (ImageSharp) | Neu (Magick.NET) |
|---|---|
| `Image.LoadAsync(stream)` | `new MagickImage(stream)` (liest das erste Frame — wie bisher) |
| `image.Metadata.DecodedImageFormat.Name` | `image.Format` (`MagickFormat.Jpeg/Png/WebP/Gif`) |
| `image.Mutate(c => c.AutoOrient())` | `image.AutoOrient()` |
| `Resize(new ResizeOptions { Mode = Max, Size = (max, max) })` | `image.Resize(new MagickGeometry(max, max))` — ohne `IgnoreAspectRatio` = proportionales Einpassen |
| `image.SaveAsWebpAsync(output)` | `image.Format = MagickFormat.WebP; image.Write(output)` |
| `PngEncoder` / `GifEncoder` / `JpegEncoder` | `MagickFormat.Png` / `MagickFormat.Gif` / `MagickFormat.Jpeg` |
| EXIF-Entfernung durch Re-Encode | **explizit** `image.Strip()` (entfernt alle Profile/Metadaten) |
| `UnknownImageFormatException` / `InvalidImageContentException` | `MagickException` (Basisklasse aller Magick-Fehler) |

**Verhaltensgleichheit:** GIFs werden wie bisher als Einzelbild (erstes Frame) verarbeitet und als GIF gespeichert; WebP-Konvertierung gilt weiterhin nicht für GIFs; die maximale Kantenlänge (`MediaOptions.MaxEdgePixels`) und die Format-Whitelist (JPEG/PNG/WEBP/GIF) bleiben unverändert.

---

## 4. Umsetzungsschritte

1. **Paketwechsel** in [`BlogCms.Infrastructure.csproj`](../src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj:24): `SixLabors.ImageSharp` entfernen, `Magick.NET-Q8-AnyCPU` (neueste stabile Version) hinzufügen.
2. **Implementierung** in [`ImageProcessor.cs`](../src/BlogCms.Infrastructure/Media/ImageProcessor.cs): `ImageSharpImageProcessor` → `MagickImageProcessor` (Interface `IImageProcessor` und `ProcessedImage` bleiben unverändert). Ablauf:
   - Stream einlesen → `MagickImage` erzeugen (bei Fehler `MagickException` → `null`)
   - Format prüfen (Whitelist) → sonst `null`
   - `AutoOrient()` → `Resize(MagickGeometry(max, max))` → `Strip()`
   - Zielformat bestimmen (WebP bei `ConvertToWebP` und Nicht-GIF, sonst Ausgangsformat)
   - In `MemoryStream` schreiben → `ProcessedImage` mit Content-Type/Extension/Breite/Höhe
3. **DI-Registrierung** in [`Program.cs`](../src/BlogCms.Web/Program.cs:173) auf den neuen Klassennamen umstellen.
4. **Test** ergänzen: `MagickImageProcessorTests` — ein kleines PNG programmatisch erzeugen, verarbeiten und prüfen (WebP-Ausgabe, PNG-Ausgabe, ungültige Daten → `null`, Größenbegrenzung).
5. **Verifikation:** `dotnet build` (0 Fehler), komplette Testsuite, `dotnet list package --vulnerable` (keine neuen Warnungen).

---

## 5. Risiken und Hinweise

- **Native Bibliotheken:** Magick.NET liefert die nativen Libs per NuGet mit — im Docker-Image ist **keine** zusätzliche System-Installation nötig.
- **Speicher/Temp-Dateien:** Magick.NET kann bei sehr großen Bildern Temp-Dateien nutzen; die bestehenden Upload-Limits ([`MediaOptions`](../src/BlogCms.Infrastructure/Media/MediaOptions.cs)) begrenzen die Eingangsgröße bereits.
- **EXIF:** wird jetzt explizit per `Strip()` entfernt (statt implizit durch Re-Encode) — das ist strenger und dokumentiert.
- **Lizenz:** Magick.NET steht unter Apache-2.0; die native ImageMagick-Bibliothek ebenfalls — keine kommerziellen Einschränkungen.

---

## 6. Abnahmekriterien

- [x] `SixLabors.ImageSharp` kommt in keiner `.csproj` und keiner Quelldatei mehr vor.
- [x] `dotnet build` ist fehlerfrei; alle Tests sind grün (inkl. neuem Processor-Test).
- [x] Ein JPEG/PNG/WebP/GIF-Upload wird weiterhin korrekt verarbeitet (Format-Whitelist, Größenbegrenzung, WebP-Konvertierung, EXIF-Entfernung).
- [x] Ungültige Bilddaten führen weiterhin zu `null` (kein Absturz, saubere Fehlermeldung im Upload-Workflow).
- [x] `dotnet list package --vulnerable` meldet keine neuen Schwachstellen.

---

## 7. Umsetzungsstand

**Erledigt:**

1. **Paketwechsel** — `SixLabors.ImageSharp` 4.1.3 entfernt, `Magick.NET-Q8-AnyCPU` **15.0.0** hinzugefügt ([`BlogCms.Infrastructure.csproj`](../src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj)).
2. **Implementierung** — [`ImageProcessor.cs`](../src/BlogCms.Infrastructure/Media/ImageProcessor.cs) enthält jetzt `MagickImageProcessor`; Interface `IImageProcessor` und `ProcessedImage` sind unverändert.
3. **DI** — [`Program.cs`](../src/BlogCms.Web/Program.cs) registriert `IImageProcessor → MagickImageProcessor`.
4. **Tests** — neu: [`MagickImageProcessorTests`](../tests/BlogCms.Tests/MagickImageProcessorTests.cs) mit 5 Tests (WebP-Konvertierung, PNG-Erhalt, proportionale Verkleinerung, GIF-Sonderfall, ungültige Daten → `null`).
5. **Verifikation** — Build 0 Fehler; **165/165 Tests grün**; `dotnet list package --vulnerable` meldet für alle vier Projekte keine anfälligen Pakete.

**Wichtige Erkenntnis (Bug beim ersten Testlauf):**

`image.Resize(new MagickGeometry(max, max))` **vergrößert** kleinere Bilder ebenfalls (100×50 → 1600×800), während ImageSharps `ResizeMode.Max` ausschließlich verkleinert hat. Die Implementierung prüft daher vor dem Resize:

```csharp
var maxEdge = (uint)_options.MaxEdgePixels;
if (image.Width > maxEdge || image.Height > maxEdge)
{
    image.Resize(new MagickGeometry(maxEdge, maxEdge));
}
```

Damit ist das Verhalten wieder deckungsgleich mit dem bisherigen ImageSharp-Processor.
