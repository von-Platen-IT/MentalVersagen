# Mobile-Navigation — Auffälliger Menü-Button & Klarer Drawer

**Ziel:** Die mobile Navigation klar sichtbar und gut bedienbar machen — ohne neue
Skripte, nur CSS. Maximale Browser-Kompatibilität, bestehende Bootstrap-Mechanik
(Collapse + Dropdown) wird weiterverwendet.

**Leitbild:** Der Menü-Button ist der Eingang zur Akte — er muss im Dunkeln leuchten.

---

## 1. Betroffene Dateien

| Datei | Änderung |
|---|---|
| [`_Layout.cshtml`](../src/BlogCms.Web/Pages/Shared/_Layout.cshtml) | Toggler-Markup: Icon raus, Text „Menü" rein |
| [`site.css`](../src/BlogCms.Web/wwwroot/css/site.css) | Hauptarbeit: Button, Glow, Drawer-Kästen, User-Untermenü |

Keine Backend-/Logik-/JS-Änderung. Bootstrap-JS bleibt unverändert.

---

## 2. Menü-Button (mobil)

- Standard-`navbar-toggler-icon` entfällt; stattdessen sichtbarer Text **„Menü"**.
- Roter Button (`--mv-red`), uppercase Mono-Typografie, Mindesthöhe 44 px.
- **Wabernder Glow:** `@keyframes` pulsiert die `box-shadow` weich zwischen
  dezent und kräftig (unregelmäßiger Rhythmus wirkt „wabernd").
- Kontrast: Tinte/Schwarz auf Rot bzw. Weiß auf Rot (≥ 4,5:1).
- `@media (prefers-reduced-motion: reduce)` schaltet die Animation ab
  (greift zusätzlich über die globale Reduced-Motion-Regel).
- `@media (hover: hover)` verstärkt den Glow bei Hover/Fokus.

## 3. Drawer & Nav-Links (mobil, ≤ 991.98px)

- Der aufgeklappte Bereich erhält eine eigene Fläche (dunkel) mit Abstand.
- Jeder Hauptlink wird ein **dunkelgrauer Kasten mit schwarzem Rand**:
  volle Breite, 1px schwarzer Rahmen, kleiner Radius, ausreichend Innenabstand.
- Aktiver Link: roter Akzent (linker Balken/Rand) statt Unterstreichung.
- Der bestehende `::after`-Unterstreichungs-Effekt wird mobil deaktiviert.
- Touch-Flächen ≥ 44 px, kein horizontaler Scroll.

## 4. Benutzer-Menü (mobil, eingeloggt)

- Das Dropdown wird mobil zum **eingeschobenen Untermenü**: `position: static`,
  volle Breite, linker Einzug, Kasten-Optik analog zu den Nav-Links.
- Aufklappen weiterhin über Bootstrap (`data-bs-toggle="dropdown"`, `show`-Klasse) —
  kein eigenes Skript. Desktop behält das schwebende Panel.
- Der Caret dreht sich bereits über `aria-expanded="true"`.

## 5. Kompatibilität & Barrierefreiheit

- Nur Standard-CSS (Flexbox, box-shadow, animation, media queries) — kein
  `:has()`, kein Container-Query, keine neuen JS-Abhängigkeiten.
- Hover-Effekte nur unter `@media (hover: hover)`.
- Toggler behält `aria-expanded`/`aria-controls`; Fokus bleibt sichtbar.
- Kontraste werden gegen `#171b24`/`#d21f3c` geprüft (AA).

---

## 6. Akzeptanzkriterien

- [ ] Mobil ist der Menü-Button als roter Button mit „Menü" und Glow deutlich sichtbar.
- [ ] Drawer-Links erscheinen als dunkelgraue Kästen mit schwarzem Rand.
- [ ] Eingeloggtes User-Menü erscheint als eingeschobenes Untermenü im Drawer.
- [ ] `prefers-reduced-motion` deaktiviert die Glow-Animation.
- [ ] Desktop-Navigation und alle Funktionen unverändert; Build/Tests grün.
- [ ] Kein neues JavaScript, keine neuen Abhängigkeiten.
