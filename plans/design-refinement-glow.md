# Design-Refinement — „Aktenlicht": Atmosphärische Glow- & Lichteffekte

**Stilrichtung:** Atmosphärisch-dezent — sanfte Glows, Hover-Lift, subtile Spotlight-Gradienten.
**Leitbild:** Die Akte liegt im Dunkeln; ein einzelner Lichtkegel macht sie lesbar.
**Grundsätze:** Maximale Kompatibilität (nur CSS/Standard-JS, keine Frameworks), WCAG AA bleibt
erhalten, alle Effekte hinter `prefers-reduced-motion`, keine Layout-Shifts, Mobile bleibt
performant (nur `transform`/`opacity` animieren).

---

## 1. Betroffene Dateien

| Datei | Änderung |
|---|---|
| [`site.css`](../src/BlogCms.Web/wwwroot/css/site.css) | Hauptarbeit: Tokens, Glows, Hover-Lift, Spotlight, Animationen |
| [`site.js`](../src/BlogCms.Web/wwwroot/js/site.js) | Scroll-Reveal via IntersectionObserver (progressive enhancement) |
| [`_Layout.cshtml`](../src/BlogCms.Web/Pages/Shared/_Layout.cshtml) | Akzentfont *Special Elite* ergänzen, `js`-Klasse auf `<html>` |
| [`_PageHeader.cshtml`](../src/BlogCms.Web/Pages/Shared/_PageHeader.cshtml) | keine Änderung nötig — Spotlight läuft über `::after` in CSS |
| [`Index.cshtml`](../src/BlogCms.Web/Pages/Index.cshtml) | ggf. kleine Klassen für Hero-Entrance (optional, CSS-first) |

Keine Backend-/Logikänderungen. Build und Tests bleiben unberührt.

---

## 2. CSS-Design-Tokens (Erweiterung in `:root`)

```css
/* Glow & Licht */
--mv-glow-red: 0 0 22px rgba(210, 31, 60, 0.30);
--mv-glow-red-soft: 0 0 14px rgba(210, 31, 60, 0.18);
--mv-glow-sepia: 0 0 18px rgba(199, 180, 139, 0.22);
--mv-shadow-lift: 0 14px 34px rgba(0, 0, 0, 0.55), 0 4px 12px rgba(0, 0, 0, 0.4);
--mv-ease: cubic-bezier(0.22, 0.61, 0.36, 1);
--mv-spot: radial-gradient(ellipse 60% 45% at 50% 0%, rgba(199, 180, 139, 0.14), transparent 70%);
--mv-font-stamp: "Special Elite", "Courier New", monospace;
```

---

## 3. Ambient-Hintergrund (Body)

- `body::before`: fixierter, sehr dezenter radialer Rot-Schein oben links +
  Blau-Schein unten rechts (`opacity` ~0.5, `pointer-events: none`, `z-index: -1`).
- `::selection`: rote Tönung (`background: rgba(210,31,60,.55); color: #fff`).
- Custom Scrollbar: `scrollbar-color` (Standard) + `::-webkit-scrollbar` (Chrome/Safari).

## 4. Typografie-Feinschliff

- **Neuer Akzentfont** *Special Elite* (Typewriter-Look, passt zum Akten-Thema)
  ausschließlich für: `.mv-stamped`, `.hero-tagline`, `.mv-filter__head`-Bereich optional.
  Fallback `Courier New` → kein CDN-Zwang.
- Hero-Titel: weiches Sepia-`text-shadow` (Lichtkegel-Gefühl statt hartem Offset).
- Headlines: feiner Glow nur im Page-Header (dort dunkler BG → gut lesbar).

## 5. Karten (`.mv-case`) — Hover-Lift & Kanten-Glow

- `transition: transform .35s var(--mv-ease), box-shadow .35s, border-color .35s`
- Hover/Fokus-mit-Tastatur: `translateY(-5px)` + `--mv-shadow-lift` +
  `box-shadow`-Anteil `--mv-glow-red-soft` + Border hellt auf.
- Titelbild: `transform: scale(1.04)` mit `overflow: hidden` am Link-Wrapper.
- Rotation (`--case-rot`) bleibt erhalten — Lift wirkt zusätzlich.
- `@media (hover: none)`: Effekte deaktiviert (Touch-Geräte).

## 6. Buttons / Badges / Pills

- `.btn-primary`: dezenter Gradient-Fill (`--mv-red` → `--mv-red-dim`),
  Hover: `--mv-glow-red-soft`, `:active`: `translateY(1px)` (Press-Feedback).
- `.mv-badge`: Hover mit farbigem Glow passend zur Badge-Farbe (via `currentColor`).
- `.mv-pill`: Hover: Border hellt + minimales Lift, sanfter Sepia-Glow.
- Fokus-Glow bei Formularen weicher: `box-shadow: 0 0 0 3px rgba(210,31,60,.22), var(--mv-glow-red-soft)`.

## 7. Page-Header / Hero — Spotlight & Vignette

- `.page-header::after`: `--mv-spot` (Sepia-Lichtkegel von oben) + Vignette
  (`radial-gradient(transparent 60%, rgba(7,9,13,.5))`), `pointer-events: none`.
- Hero-Logo: Glow via `filter: drop-shadow(0 0 18px rgba(199,180,139,.25))`
  zusätzlich zum bestehenden harten Schatten.
- **CRT-Scanlines (Zusatzwunsch):** dunkle, dezente horizontale Linien
  (`repeating-linear-gradient`, 1px auf 3px, `rgba(7,9,13,.20)`) als dritte
  Ebene im Header-Overlay — gibt dem Hero den leichten Archiv-/Überwachungslook.
- Entrance-Animation (CSS-only): Hero-Inhalt `fade-in-up` (0.6s, `var(--mv-ease)`),
  gestaffelt via `animation-delay` auf Tagline/CTAs.
- Mobile-Rail-Header (≤576px) bleibt unangetastet.

## 8. Navbar & Footer

- Navbar: 1px Glow-Hairline unten (`box-shadow: 0 1px 0 rgba(210,31,60,.25)`),
  aktive Nav-Linie bekommt `--mv-glow-red-soft`.
- Footer-Links: Hover mit sanftem Aufleuchten (Farbe + minimaler Glow).

## 9. Scroll-Reveal (site.js — progressive enhancement)

- `<html>` erhält Klasse `js` (bereits via Layout oder inline `<script>` im Head).
- IntersectionObserver: `.mv-case`, `.mv-doc`, `.mv-teaser` erhalten `.mv-reveal`,
  sichtbar → Klasse `is-visible` (opacity 0→1, `translateY(14px)` → 0, 0.5s).
- **Fallback-Kette:** ohne JS → alles sichtbar (Selektor `html.js .mv-reveal`);
  `prefers-reduced-motion` → keine Transition; `IntersectionObserver` fehlt → sofort sichtbar.
- Stagger: `transition-delay` via `nth-child` (max. 3 Karten versetzt).

## 10. Mobile & Performance

- Nur `transform`/`opacity`-Animationen (GPU-freundlich, kein Reflow).
- Hover-Effekte nur bei `@media (hover: hover)`.
- Ambient-Layer: ein fixiertes Pseudo-Element, kein zusätzliches DOM/JS.
- Rail-Header-Logik (≤576px) wird nicht verändert.

## 11. Barrierefreiheit (unverändert gültig)

- Kontraste bleiben ≥ AA (Glows sind rein dekorativ, kein Text-Kontrastverlust).
- Alle Animationen respektieren `prefers-reduced-motion: reduce`.
- Fokus-Outline bleibt sichtbar (Glow ersetzt es nicht, ergänzt es).

---

## 12. Akzeptanzkriterien

- [ ] Karten heben sich beim Hover mit weichem Schatten + dezentem rotem Kanten-Glow.
- [ ] Hero zeigt Sepia-Spotlight + Vignette; Logo hat sanften Glow.
- [ ] Buttons geben Press-Feedback und leuchten dezent beim Hover.
- [ ] Stempel/Taglines nutzen Typewriter-Font *Special Elite*.
- [ ] Scroll-Reveal funktioniert, Inhalt ohne JS vollständig sichtbar.
- [ ] Hero zeigt dezente dunkle CRT-Scanlines (auch mobil deaktiviert mit Header-Effekten).
- [ ] `prefers-reduced-motion` deaktiviert alle Bewegung/Glows-Animationen.
- [ ] Mobile (Rail-Header, Touch) unverändert funktional; keine horizontalen Scrolls.
- [ ] Build grün, keine Logik-/Backend-Änderungen.