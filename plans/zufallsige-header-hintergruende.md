# Zufällige Header-Hintergründe

**Status:** umgesetzt · **Datum:** 2026-10-20

## Ziel

Der Seiten-Header (`_PageHeader.cshtml`) zeigte bisher immer eines von drei
festen Motiven, je nachdem welche „Slide" die Seite im Model gesetzt hat. Die
neuen Motive (`*_bg.jpg` / `*_bg.webp`, 2560 × 1097) sollen als **zusätzliche
Hintergründe** dienen und **zufällig** erscheinen — also bei jedem
Seitenaufruf ein anderes Bild zeigen.

## Umsetzung

### Motiv-Pool

[`PageBackgrounds`](../../src/BlogCms.Web/Content/PageBackgrounds.cs) (neu, in
`BlogCms.Web.Content`) hält den Motiv-Pool und wählt per `Random.Shared`
eines aus:

- die drei klassischen Header-Slides (`header_slides_1..3.png`)
- alle WebP-Motive aus `wwwroot/pics/` (WebP ist deutlich kleiner als die
  JPEG-Quelle: z. B. 318 KB → 186 KB bei identischer Darstellung)

**Neue Motive fließen automatisch ein:** Der Pool wird einmalig beim ersten
Zugriff aufgebaut und enthält neben den Slides **alle `*.webp`-Dateien**, die im
Bildverzeichnis der Anwendung liegen. Ein neues Bild muss also nur als WebP
nach `wwwroot/pics/` gelegt werden — keine Codeänderung nötig. Gesucht wird in:

1. `wwwroot/pics/` neben der ausgelieferten DLL (Publish-Output, Container)
2. `wwwroot/pics/` im Projektordner (Development: `bin/<config>/<tfm>`)
3. dem Pfad aus der Umgebungsvariable `MV_PICS_DIR` (Override)

Die statische Liste im Code dient als Rückfallebene (z. B. in Tests) und
dokumentiert den Bestand.

```csharp
public static string Next() => All[Random.Shared.Next(All.Count)];

public static string ForSlide(int slide) => slide is >= 1 and <= 3
    ? Slides[slide - 1]
    : Next();
```

### Header-Partial

[`_PageHeader.cshtml`](../../src/BlogCms.Web/Pages/Shared/_PageHeader.cshtml)
setzt die CSS-Variable `--ph-img` jetzt aus dem Pool:

```razor
var background = PageBackgrounds.ForSlide(Model.Slide);
<header class="page-header" style="--ph-img: url('@background');">
```

Die Auswahl erfolgt **serverseitig** — das Motiv steht damit im HTML und wird
auch ohne JavaScript dargestellt (kein Nachladen, kein Flackern).

### Model

[`PageHeaderModel.Slide`](../../src/BlogCms.Web/Models/PageHeaderModel.cs) hat
jetzt den Standardwert `0` = „zufällig". `Slide` 1–3 erzwingt weiterhin eine
feste klassische Slide.

### Views

Die festen `Slide = n,`-Zuweisungen wurden aus 21 Views entfernt (22 Stellen),
damit überall der Zufall gilt. Betroffen waren u. a. Kontakt, Suche,
Impressum, Datenschutz, Newsletter, Account-Seiten, Moderation und alle
Admin-Seiten.

### Startseite

Die Startseite (`Index.cshtml`) behält bewusst ihr eigenes Landing-Motiv
(`background_landing_page.png`) — es ist die Markenkomposition der Startseite
und kein Header-Motiv der Unterseiten. Sie nutzt das Partial nicht, sondern
setzt `--ph-img` direkt.

## Verifikation

- 20 Aufrufe von `/Kontakt` lieferten 15 verschiedene Motive, 40 Aufrufe von
  `/Suche` 21 verschiedene (Pool: 30) — die Auswahl ist gleichverteilt und
  tatsächlich zufällig.
- Sichtprüfung Desktop (`/Kontakt`, `/Suche`, `/Impressum`) und Mobile
  (Grafikband, 390 px): Motiv, Titel, Logo und Vignette wie beim bisherigen
  Header-Design.
- Auslieferung geprüft: `/pics/*.webp` → `200 image/webp`.
- Tests [`PageBackgroundsTests`](../../tests/BlogCms.Tests/PageBackgroundsTests.cs)
  (Pool, feste Slide, Zufall, automatische Aufnahme aller WebP-Dateien des
  Web-Projekts) — 211 Tests grün.

## Hinweise

- Die JPEG-Varianten der Motive liegen weiterhin in `pics/` und
  `wwwroot/pics/`. Ausgeliefert wird die WebP-Variante; die JPEG-Dateien sind
  Redundanz und könnten aus `wwwroot/pics/` entfernt werden.
- Neue Motive: Datei als WebP nach `wwwroot/pics/` legen — sie wird beim
  nächsten Start automatisch in die Auswahl aufgenommen. Für die
  Rückfallebene (Tests ohne Bildverzeichnis) den Pfad zusätzlich in
  `PageBackgrounds.KnownMotifs` eintragen.
