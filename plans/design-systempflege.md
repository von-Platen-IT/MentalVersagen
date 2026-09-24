# Optik-Session — Systempflege & Qualität

**Fokus:** Kein neuer Look, sondern den bestehenden Akten-Look *sauber, konsistent und
barrierefrei* machen. CSS-first, minimalinvasives HTML, keine Backend-/Logikänderung.

**Leitprinzip:** Ein Design-System ist nur so gut wie seine Disziplin — keine
Inline-Styles, keine Duplikate, keine toten Regeln, geprüfte Kontraste.

---

## 1. Ausgangslage

Der Akten-Look ist umgesetzt (Tokens, Glow, Karten, Header, Filter, Lightbox in
[`site.css`](../src/BlogCms.Web/wwwroot/css/site.css)). Verbleibende Qualitätslücken:

| Fund | Ort | Auswirkung |
|---|---|---|
| Tote Bootstrap-Default-CSS | [`_Layout.cshtml.css`](../src/BlogCms.Web/Pages/Shared/_Layout.cshtml.css) | blaue Links (`#0077cc`), `.footer`-Regeln, potenzielle Konflikte |
| 12 Inline-Styles | Details, Moderation, Newsletter, Donate, LinkLists, Index, _PageHeader | bricht HTML/CSS-Trennung |
| Dupliziertes Karten-Markup | Index + Suche | doppelte Wartung |
| Dupliziertes Header-Markup | [`LinkLists/Details.cshtml`](../src/BlogCms.Web/Pages/LinkLists/Details.cshtml) | inkonsistent zum Partial |
| Bootstrap `text-danger` | Login, Register, Suche | Kontrast ungeprüft auf dunklem Grund |
| Kein Print-Stylesheet | — | Dokument-Look verschenkt |
| Kein `color-scheme`, kein `scroll-padding-top`, kein `aria-current` | Layout | UX/A11y-Lücken |

---

## 2. Arbeitsschritte

### 2.1 Aufräumen & Entkopplung
- [`_Layout.cshtml.css`](../src/BlogCms.Web/Pages/Shared/_Layout.cshtml.css) leeren bzw.
  entfernen (Scoped-CSS wird automatisch gebündelt; Inhalt ist veraltet).
- Inline-Styles in benannte Klassen überführen:
  - `style="white-space: pre-wrap;"` → `.mv-preline`
  - `style="cursor:pointer;"` → `.mv-summary`
  - `style="max-width:460px/520px;"` → `.mv-form--narrow` / `.mv-form--medium`
  - Ausnahme: dynamisches `style="--ph-img: url(...)"` bleibt (legitimer Datenwert).

### 2.2 Markup-Konsolidierung
- Neues Partial [`_ArticleCard.cshtml`](../src/BlogCms.Web/Pages/Shared/_ArticleCard.cshtml)
  kapselt das identische Akten-Karten-Markup.
- [`Index.cshtml`](../src/BlogCms.Web/Pages/Index.cshtml) und
  [`Suche.cshtml`](../src/BlogCms.Web/Pages/Suche.cshtml) nutzen das Partial.
- [`LinkLists/Details.cshtml`](../src/BlogCms.Web/Pages/LinkLists/Details.cshtml):
  Duplikat-Header im Nicht-gefunden-Fall durch `_PageHeader`-Partial ersetzen.
- [`site.js`](../src/BlogCms.Web/wwwroot/js/site.js) `renderCard()` auf identische
  Klassennamen prüfen (Server- und Client-Rendering müssen deckungsgleich bleiben).

### 2.3 Tokens & Kontrast-Audit
- Alle Farbpaare prüfen (Badge-Farben `#67c4c9`, `#d8a24a`, `#7aa2d1`, `--mv-text-dim`
  auf Card, `--mv-ink` auf Rot). Ziel: WCAG AA ≥ 4,5:1 für Text.
- Bootstrap `text-danger` → eigene `.mv-text-danger` mit geprüftem Kontrast.
- `color-scheme: dark` auf `:root` + `<meta name="color-scheme">`.

### 2.4 Print-Stylesheet
- `@media print` in `site.css`: Papier-Look, Nav/Footer/Glow/Scanlines aus,
  schwarze Schrift auf Weiß, Links mit sichtbarer URL, Karten ohne Rotation.

### 2.5 A11y-Feinschliff
- `scroll-padding-top` passend zur Sticky-Navbar (Anker `#main-content`, `#comments`).
- `aria-current="page"` für aktive Navigation (TagHelper `asp-page` + aktiver Zustand).
- Fokus-Reihenfolge, `:focus-visible`, Alt-Texte und Landmarken gegenprüfen.

### 2.6 Verifikation
- `dotnet build` und Tests grün.
- Browser-Check Desktop/Mobile, Tastatur-Navigation, Kontrast-Check, Print-Preview.

---

## 3. Akzeptanzkriterien

- [ ] Keine statischen Inline-Styles mehr in den Views (außer dynamischem `--ph-img`).
- [ ] Karten-Markup existiert nur noch einmal (Partial), Index/Suche nutzen es.
- [ ] [`_Layout.cshtml.css`](../src/BlogCms.Web/Pages/Shared/_Layout.cshtml.css) ist entfernt/leer.
- [ ] Alle Text-Kontraste ≥ WCAG AA; `text-danger` ersetzt.
- [ ] Print-Ansicht zeigt Akten-Dokument-Look ohne Navigation/Glow.
- [ ] Aktive Navigation via `aria-current`, Anker springen unter die Navbar.
- [ ] Build und Tests unverändert grün, keine Logik-/Backend-Änderung.

---

## 4. Bewusste Nicht-Ziele

- Keine neuen Komponenten oder Layouts (das war Option „Breite Politur").
- Kein Umbau der Bild-Hintergründe zu `<img>` (Header-Motiv bleibt CSS-Background).
- Keine Datenmodell-/Service-Änderungen.
