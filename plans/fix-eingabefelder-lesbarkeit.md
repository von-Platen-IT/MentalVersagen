# Eingabefelder: schwarzer Text auf dunkelgrauem Grund

**Status:** umgesetzt (Variante A — nur Lesbarkeit, dunkelgraue Felder bleiben)
**Datum:** 2026-10-20

## Problem

Die Anwendung ist durchgehend dunkel, einzelne Flächen sind als helles „Papier"
gestaltet (`.mv-doc`). Native Bedienelemente erben in diesen Papierflächen die
schwarze Papier-Schrift (`--mv-ink`, #0b0c0e), werden aber vom Browser
(`color-scheme: dark` auf `html`) mit dunkelgrauem Feldgrund gezeichnet.
Ergebnis: **schwarzer Text auf dunkelgrauem Grund** — unlesbar.

Dasselbe Muster tritt bei Karten und Knöpfen auf, die innerhalb einer
Papierfläche liegen (dunkler Kasten auf hellem Papier): Die Papier-Regeln
setzen dort schwarze Schrift bzw. zu dunkle Buttonfarben.

## Befunde (Analyse über alle `.cshtml` und die drei Stylesheets)

### Eingabefelder

| Ort | Element | Ursache |
|---|---|---|
| `Pages/Articles/Details.cshtml` (Kommentar-Upload, 2×) | `<input type="file" class="form-control">` | Datei-Felder übernehmen die Schrift nicht aus `.form-control`; sie erben die Papier-Schrift |
| `Pages/Admin/Articles/Edit.cshtml` (Bilderverwaltung) | `<input type="text" readonly>` (Markdown-Snippet) | Feld ohne CSS-Klasse → Browser-Standard (dunkelgrau), Schrift geerbt (schwarz) |
| `Pages/Kontakt.cshtml`, `Pages/Account/Register.cshtml` (Honeypot) | `<input type="text">` | wie Snippet-Feld, nur ausgeblendet |

### Knöpfe und Karten auf dunklem Grund innerhalb von Papierflächen

| Ort | Element | Ursache |
|---|---|---|
| `Pages/Kontakt.cshtml`, `Pages/Account/Register.cshtml` (Sicherheitsabfrage) | `.btn-outline-secondary` („Neue Aufgabe") | Papier-Regel setzt schwarzen Text, der Knopf sitzt im dunkelgrauen Captcha-Kasten |
| `Pages/Admin/Articles/Edit.cshtml` (Bilderverwaltung) | `.btn-outline-secondary`, `.btn-outline-danger` | wie oben, zusätzlich zu dunkles Rot auf dunkelgrauem Grund |
| `Pages/Unterstuetzer/Index.cshtml` (Abo-Karte) | `.mv-case` mit Text, Links und Knöpfen | Die Aktenkarte erbt die schwarze Papier-Schrift, liegt aber auf dunkelgrauem Grund |

## Lösung (Variante A)

1. **Grundregel in `site.css`** — alle Text-Eingabefelder bekommen eine helle
   Schrift, unabhängig von der umgebenden Fläche:
   ```css
   input:not([type="hidden"]), select, textarea { color: var(--mv-text); }
   ```
   Die Spezifität (0,1,1) bleibt bewusst niedrig, damit die hellen Papierfelder
   der Akte-Verwaltung (`.mv-create .form-control` in `article-create.css`,
   0,2,0) weiterhin gewinnen — dort bleibt die Schrift dunkel auf weißem Feld.

2. **Datei-Felder explizit** in `site.css`: dunkelgrauer Feldgrund
   (`--mv-bg-card`), helle Schrift, lesbarer Auswahl-Knopf
   (`::file-selector-button`).

3. **`.mv-honeypot`** erhält in `site.css` helle Schrift und dunklen Grund.

4. **Dunkle Kästen auf Papierflächen** (`.mv-case`, `.mv-captcha`,
   `.mv-admin-media`) erhalten in `site.css` helle Schrift; Outline-Knöpfe
   dort bekommen helle Schrift und hellen Rand, der Danger-Knopf ein helles
   Rot (`--mv-red-text`).

5. **`article-create.css`**: die helle Papierfläche (`.mv-create`) behält
   ausdrücklich dunkle Schrift auf weißem Feld — die neue Grundregel darf sie
   nicht umbiegen (Datei-Felder gesondert genannt).

6. **`Admin/Articles/Edit.cshtml`**: das Snippet-Feld bekommt
   `class="form-control form-control-sm"` und damit das einheitliche
   dunkelgraue Feld mit heller Schrift.

## Verifikation

**Berechnete Farben** (gemessen im Browser, Playwright):

| Kontext | Text | Grund |
|---|---|---|
| Dunkle Flächen (`.form-control`, `.form-select`, `textarea`, Datei-Feld) | `rgb(231,228,219)` hell | `rgb(23,27,36)` dunkelgrau |
| `.mv-doc` (Papier: Login, Kontakt, Newsletter, Admin) | hell | dunkelgrau |
| `.mv-create` (Akte anlegen/bearbeiten) | `rgb(11,12,14)` dunkel | `rgb(255,255,255)` weiß |
| Druckansicht (`@media print`) | `rgb(0,0,0)` schwarz | `rgb(255,255,255)` weiß |

**Kontrast-Audit** über 17 Seiten (Startseite, Suche, Login, Registrierung,
Kontakt, Newsletter, Datenschutz, Impressum, Unterstützer, Artikelübersicht,
alle Admin-Seiten, Moderation, Akte anlegen/bearbeiten): Es gibt **keinen
dunklen Text auf dunklem Grund mehr**. Vor der Korrektur lagen die betroffenen
Stellen bei 1,14:1 bis 1,89:1.

**Live geprüft** an der laufenden Anwendung (Postgres-Container, Port 5099)
mit temporärem Test-Administrator; der Testzugang wurde danach wieder
gelöscht.

## Bewusst nicht geändert

Die Eingabefelder auf den Papierflächen bleiben dunkelgrau (Variante A). Ein
Umstellen auf durchgehend helle Felder (wie im Akte-Formular) wäre ein
sichtbarer Designumbau und wurde nicht beauftragt. Ebenso bleiben Labels,
Hilfetexte und gedämpfte Texte auf Papierflächen unverändert (hellgrau auf
weiß, 2,28:1) — das ist ein anderes Kontrastthema und war ausdrücklich nicht
Gegenstand des Auftrags.
