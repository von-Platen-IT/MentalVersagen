# Design-Konzept — Mentalversagen Blog

**Stil:** Investigativer Retro-Dokumentarstil × moderner Verschwörungsthriller
**Leitbild:** Digitales Recherchearchiv — „Fakten vs. Behauptungen · Licht vs. Schatten"
**Ziel:** Preiswürdige Optik, gute Benutzbarkeit an erster Stelle, Mobile First, WCAG-konform.

---

## 1. Design-Tokens (Farbwelt)

| Token | Farbe | Hex | Verwendung |
|---|---|---|---|
| `--mv-bg-deep` | Tiefschwarz | `#07090D` | Seitengrund |
| `--mv-bg-card` | Anthrazit | `#171B24` | Karten-/Content-Flächen |
| `--mv-bg-elev` | Dunkelblau | `#18293E` | technische/politische Elemente, Header-Töne |
| `--mv-text` | Entsättigtes Weiß | `#E7E4DB` | Fließtext hell |
| `--mv-text-dim` | Grauton | `#9A978F` | Meta (Datum, Autor) |
| `--mv-sepia` | Sepia/Dokument | `#C7B48B` | historische Dokumente, Akten-Frames |
| `--mv-red` | Signalrot | `#D21F3C` | sparsam: Markierungen, Stempel, Nav-Aktivzustand, CTAs |
| `--mv-red-dim` | gedämpftes Rot | `#A01530` | Hover/Lineale |
| `--mv-ink` | Druck-Schwarz | `#0B0C0E` | Text auf hellen „Dokument"-Flächen |
| `--mv-paper` | helles Dokument | `#E8E3D6` | Kontrastflächen (bewussterreich hell) |

**Regel Rot:** Rot bedeutet „Hier genauer hinschauen". Keine Full-Fill,
nur Linien, Stempel, Pills, aktive Navigation, Hover-Glow.

### Kontrastprüfung (Textlänge, WCAG AA ≥ 4,5:1)
- `#E7E4DB` auf `#07090D`: ≈ 15,5:1 ✔
- `#9A978F` auf `#07090D`: ≈ 8,4:1 ✔
- `#E7E4DB` auf `#171B24`: ≈ 12,8:1 ✔
- `#D21F3C` auf `#07090D`: ≈ 4,9:1 ✔ (UI-Schwellen)
- `#0B0C0E` auf `#E8E3D6`: ≈ 15,1:1 ✔
- `#E7E4DB` auf `#18293E`: ≈ 11,2:1 ✔

---

## 2. Typografie

- **Headlines:** *Archivo Black* oder *Oswald* (kondensiert, fett, industriell,
  mit `letter-spacing: .05em`, `text-transform: uppercase`), distressed via CSS
  (`text-shadow: 2px 2px 0 #000`, leichte `mask`-Kratzer optional).
- **Fließtext:** *Inter* (gut lesbar, modern), `line-height: 1.65`.
- **Mono/Akten-Codes:** *JetBrains Mono* oder System-Mono für Aktennummern,
  `font-variant-numeric: tabular-nums`.
- **Skalierung:** `clamp()`-basierte fluid Typografie:
  H1 `clamp(1.9rem, 4vw, 3rem)`, H2 `clamp(1.5rem, 3vw, 2.1rem)`,
  Body `clamp(0.98rem, 0.9rem + 0.2vw, 1.05rem)`.
- **Quellen:** Google Fonts via `<link>` (mit preconnect); Fallbacks //
  System-Fonts (kein Blocker).

---

## 3. Assets (Bilder)

| Datei | Zweck | Plattform |
|---|---|---|
| `pics/Mentalversagen_schriftzug.png` | **Logo/Mschmutzbrand** | in Navbar, Hero, Header |
| `pics/background_landing_page.png` | Hero-BG Landing | Landing Page Hero |
| `pics/header_slides_1.png` | Header-BG (Kategorie/Header wechseln) | Headerbereich |
| `pics/header_slides_2.png` | Header-BG | Headerbereich |
| `pics/header_slides_3.png` | Header-BG | Headerbereich |

- Kopie in `wwwroot/pics/` (als statische Assets).
- **Hero (Landing):** `background_landing_page.png` als
  `background-image` + Gradual-Dark-Overlay; Logo mit Schlagschatten
  (`filter: drop-shadow(0 4px 10px rgba(0,0,0,.1) drop-shadow(2px 2px 0 rgba(0,0,0,.6)))`).
- **Header (andere Seiten):** nutzt einen der drei Slides als BG (Page-Kontext
  schaltet `1|2|3`), darüber Logo mit Schlagschatten + Seitentitel.
- **Bild-Dekor:** feines Filmkorn (SVG-Noise als Overlay-Pseudo-Element),
  dunkel abdunkeln. Keine überladene Collage.

---

## 4. Komponenten

### 4.1 Navigation (dunkle Leiste, rote Akzentlinie)
- Vollbreite: `#07090D` + `border-bottom: 1px solid #222` +
  unter dem Menüpunkt dünne rote Linie (aktiv: füllend; hover: Linie erscheint von links).
- Logo links, Menüpunkte rechtes: START, JFK, 9/11, MONDLANDUNG, ROSWELL,
  ILLUMINATI, COVID-19, DEEP STATE (ebenfalls admin-einträge rechts + Auth-Links).
- Mobile: Hamburger-Icon und ausklappbarer Vollbreiten-Drawer; gleiche
  Rot-Linie-Logik, linksbündig.

### 4.2 Hero / Landing Page
- Vollbreite Hero-Sektion mit `background_landing_page.png` (cover, dark
  gradient overlay + noise).
- Logo Bild zentriert (max. ~560 px), Schlagschatten, darunter Tagline
  „Fakten. Behauptungen. Was bleibt übrig?" — Mono-Obertitel.
- CTA-Ton: zwei hervorstechende Buttons (Aktuelle Akten · Mitglied werden).
- Unten: Kategorien-Chips (Politik/Satire/Verschwörungstheorien) als
  „Recherche-Orte" mit Karten-Zuordnung.

### 4.3 Header (nicht-Landing)
- Header-Band mit einem der drei Slides als BG (cover, dunkler Overlay).
- Um 90°-Gegen-Uhrzeigersinn gedrehte Version am linken Rand **auf Mobile**:
  Der Header wird zu einem vertikalem Streifen links (Höhe ~100vh), Logo
  darin gedreht (`transform: rotate(-90deg)`), Text-Pillar. Desktop = horizontal.
- Zentrale Titel + Meta (Kategorie, Nebel, Turnery).

### 4.4 Artikel-Karten („Rechercheakte")
- Anthrazit-Card, ±1.5'-Rotation (`transform: rotate(var(--rot,0))` minimal),
  dünne 1px-Border + Papier-Noise pseudo-element.
- Oben: Mono-Linie `ARCHIV <Kategorie>  #<ID-K>`, darunter Titelbild (16:9).
- Titel (condensed, upper), Kurzbeschreibung (dim).
- Badge-Reihe: `● FAKTEN`, `● THEORIEN`, `? OFFEN` (Status-Pills),
  plus `Zugriff`/`Kategorie`/`Autor`/`Datum`, rechts `WEITER →`.
- Status-Logik aus „FeatureFix1"-Daten: wird später redaktionell
  gemappt (vorerst als visueller Slot, Standard „OFFEN").

### 4.5 Status-System (redaktionell/visuell)
| Bedeutung | Farbe | Glyph |
|---|---|---|
| Belegt/Beweis | Cyan/Neutral | ● |
| Umstritten | Amber | ● |
| Offen | Hellgrau | ? |
| Widerlegt | Rot/Grau | ● |
| Dokumentiert | Sepia | ● |

(Vorläufig statisch; später optional als Feld an `Article`/`Content`.)

### 4.6 Formulare / Buttons / Badges
- Buttons: Primär = Rot (`#D21F3C`), Sekundär = Outline (Weiß/Border).
  Fokus/Outline sichtbar; `:focus-visible` mit roter Doppeltinie.
- Inputs: dunkler Fill auf dunklem BG, helle Feeling-Contrast auf Paper-Sektionen.
- Badges: `outline` + dünn, Acciatic font Mono, rot nur wichtig.
- Tolle Benutzbarkeit: große Touch-Flächen, ~44 px Min-Höhe, klare Text.

### 4.7 Login/Mitglied/Paywall
- Paywall-Teaser: Paper-Fläche `#E8E3D6` mit schwarzer Schrift,
  dadrin „GESICHERT"-Stempel (`CLASSIFIED`-Look) in Rot.
- Login/Register: ruhige, dunkle Card; deutliche Formfield-Legenden.
- Mitgliedschaft: drei Ebenen-Karten, roter CTA.

### 4.8 Footer
- Dunkler Footer, Rot-Linie oben, Links + Session-Usage, monospaced
  Akten-Budget-Text („Fakten. Hinweise. Fragen.").

---

## 5. Mobile (Mobile First)

- **Header-Rotation auf Mobile (≤576px):** Header wird verticale Linksrand
  (Postion fixed oder ToLeftToHighLess): Slide-Bild in 90° Drehung,
  InnenLogo ebenfalls `-90°`. Titel horizontal? Layout: flex column,
  `writing-mode: vertical-rl` und `transform: rotate(-90deg)` auf der
  Header-Pane ansich. Content räumt rechts für `margin`/`padding-left`.
- **Grid:** Landing-Grid `grid-template-columns: 1fr` Mobile, `repeat(3,1fr)` ≥992px.
- **Nav:** Hamburger ≤767px, Drawer Vollhöhe, Rote Linien als Unterstriche.
- **Bilder:** `max-width: 100%; height: auto;`, `object-fit: cover` in Karten/Hero.
- **Font-Grund:** `html { font-size:1em }` + `clamp()`.

---


## 6. Barrierefreiheit (WCAG)

- Kontrast ≥ 4,5:1 (Tokens oben OK), Fokus-Outline rote Doppellinie.
- Alt-Text: Logo „Mentalversagen", Slides „Deutstand Atmosphäre".
- `prefers-reduced-motion`: Hover-Glow/Noise statisch, Rotationen weg.
- `skip-link`: zur Haupt-Content-Spalte.
- Semantik: `<header>/<nav>/<main>/<footer>/<article>/<figure>` korrekt;
  überschrift hierartig, Liste als `ul`/`ol`.

---

## 7. Performance

- Slide-/Hero-Bilder mit `decoding=async`, `loading=lazy` außer Hero (`eager`).
- Kritisches CSS nicht blockierend (fonts via `preconnect`, Site-CSS).
- `font-display: swap` für Webfonts.
- SVG-Noise als inline-dataURI; kategorische Issue minimieren.

---

## 8. Implementierungs-Plan (Reihenfolge innerhalb der Code-Phase)

1. Assets-Kopie `pics/` → `src/BlogCms.Web/wwwroot/pics/` (via copy).
2. Neue Token/CSS-Datei `wwwroot/css/site.css` komplett (Design System).
3. Layout [`_Layout.cshtml`](src/BlogCms.Web/Pages/Shared/_Layout.cshtml) →
   dunkle Navbar, Logo, aktive Rot-Linie, Drawer (Mobile), Footer, Skip-Link.
4. Landing [`Pages/Index.cshtml`](src/BlogCms.Web/Pages/Index.cshtml) → Hero
   (BG + Logo + Tagline + CTAs) + Artikel-Karten im Grid.
5. Header-Komponente (Partial `_PageHeader.cshtml`): Slides-BG (wechselt),
   Logo, Titel, Meta; Mobile-rotate-Logik.
6. Artikel-Listen [`Pages/Articles/Index.cshtml`](src/BlogCms.Web/Pages/Articles/Index.cshtml)
   → Rechercheakten-Karten mit Meta/Bagas.
7. Detail [`Pages/Articles/Details.cshtml`](src/BlogCms.Web/Pages/Articles/Details.cshtml)
   → hellen Dokumenten-Leseflucht, Sidebar-Meta, Rating-Buttons als Akten-Stempel.
8. Template-Ausbau für restliche öffentlichen Seiten (Membership, Login,
   Register, Linklists, Moderation, Newsletter) mit gemeinsamen Klassen.
9. Accessibility+s:/Focus, Alt, Skip-link, `prefers-reduced-motion`.
10. Build + Build, kurzer Browser-Test (Desktop + Mobile-Breiten), Testgruppe
    erwarten keine Abänderung, Logging/Funktion unverändert.

---

## 9. Akzeptanzkriterien (Design-QA)

- [ ] Landing: Hero mit `background_landing_page.png`, Logo + Schlagschatten + Tagline.
- [ ] Header (andere Seiten): eine der drei Slides als BG, Logo vorne.
- [ ] Mobile ≤576px: Header erscheint vertikal links, 90° CCW gedreht.
- [ ] Navbar: dunkle Leiste, Logo, aktive/hover rote Linie.
- [ ] Artikel-Karten als „Rechercheakten" mit Meta + Titelbild + Status-Badges.
- [ ] Bei Text: Kontrast AA; Fokus sichtbar; Rot sparsam.
- [ ] Mobile: Layout einspaltig, Touch-Flächen ≥44 px, no-horizontal-Scroll.
- [ ] Build bleibt grün, Tests bleiben grün (designonly ändert keine Logik).
- [ ] Status-System (Belegt/Umstritten/Offen/Widerlegt) als visuelle
      Badges vorbereitet (später datengetrieben).

---

## 10. Offene Punkte / Bewusste Beschränkungen

- **Status-Daten:** Das Status-System (Belegt/Umstritten/Offen/Widerlegt) ist
  derzeit visuell mit Standardwert `OFFEN`. Eine redaktionelle Feldwahl in
  `Article` kann später ergänzt werden (kein Schema-Fortschreiben).
- **Google Fonts:** wird via CDN geladen (no-CDN-Abhängigkeits-Regel: Brand
  CI nur für Text, Fallback auf System-Fonts).
- **Hochgradige Pseudo-Elemente:** Für maximale Browser-Kompatibilität als
  `::before/::after` mit inline data-URI umgesetzt; keine externen Dienste.
- **Redaktioneller Slot:** Category-Rotation (JFK, 9/11, Mondlandung etc.)
  aktiviert aktuell die drei hart-kodierten Themen; generisch redaktionell
  via `Hashtag`/`Tag` erreichbar, nicht über Kategorie-Feld.
