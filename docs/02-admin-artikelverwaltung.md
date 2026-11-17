# Admin-Bereich: Artikelverwaltung

Diese Seite beschreibt, wie man den Admin-Bereich verwendet, um Artikel zu
erstellen, zu bearbeiten, zu veröffentlichen und zu löschen.

> Für den vollständigen Überblick über alle Verwaltungsseiten siehe
> [`12-administration.md`](12-administration.md). Eine Schritt-für-Schritt-
> Anleitung für Autoren liegt in [`13-autoren-handbuch.md`](13-autoren-handbuch.md).

**Voraussetzung:** Das eigene Konto besitzt die Identity-Rolle `Admin` oder
`Author` (siehe [`01-rollen-und-admin.md`](01-rollen-und-admin.md)). Ist man
eingeloggt, erscheint in der Navigation der Eintrag **„Verwaltung"**, der direkt
zur Artikelverwaltung führt.

- **`Admin`** sieht und bearbeitet **alle** Artikel.
- **`Author`** sieht und bearbeitet **ausschließlich eigene** Artikel; fremde
  Beiträge werden serverseitig verweigert.

## Erreichbare Seiten

| Seite | URL | Zweck |
|---|---|---|
| Übersicht | `/Admin/Articles` | Artikel (inkl. Entwürfe; für `Author` nur eigene) in einer Tabelle |
| Neu | `/Admin/Articles/Create` | Neuen Artikel anlegen |
| Bearbeiten | `/Admin/Articles/Edit?id={guid}` | Bestehenden Artikel bearbeiten |
| Linklisten | `/Admin/LinkLists` | Redaktionelle Linklisten verwalten (nur `Admin`) |

Alle Seiten sind mit der Policy `RequireAuthor` (bzw. `RequireAdmin` für
Linklisten) geschützt — ohne passende Rolle wird der Zugriff serverseitig
verweigert.

## Übersicht (`/Admin/Articles`)

Die Tabelle listet alle Artikel (sortiert nach zuletzt geändert) mit folgenden
Spalten:

| Spalte | Bedeutung |
|---|---|
| Titel | Artikeltitel |
| Kategorie | Dynamische Kategorie aus der Admin-Verwaltung (als Pille); `undefined` für nicht zugeordnete Beiträge |
| Status | `Draft`, `Published` oder `Archived` |
| Premium | `Ja`/`Nein` — ob der Artikel hinter der Paywall liegt |
| Veröffentlicht | Zeitpunkt der ersten Veröffentlichung (oder `–`) |
| Aktionen | **Bearbeiten** und **Löschen** |

Oben rechts führt **„Neuer Artikel"** zum Anlegeformular.

## Artikel anlegen (`/Admin/Articles/Create`)

Das Formular wird von derselben Feld-Partial wie die Bearbeitung gerendert.
Folgende Felder stehen zur Verfügung:

| Feld | Pflicht | Hinweise |
|---|---|---|
| **Titel** | ja | max. 300 Zeichen |
| **Slug** | nein | URL-Segment; wird sonst automatisch aus dem Titel erzeugt und bei Kollision mit `-2`, `-3` … eindeutig gemacht. Bereits veröffentlichte URLs bleiben bei Titeländerung stabil. |
| **Titelbild-URL** | ja* | externe Bild-URL; Alternative/Fallback ist der Bild-Upload (*genau eine der beiden Quellen ist Pflicht) |
| **Kurzbeschreibung / Excerpt** | ja | max. 500 Zeichen; dient als Vorschau (Artikelliste, Paywall-Teaser, RSS-Beschreibung) |
| **Kategorie** | nein | Dropdown aus den vom Admin gepflegten Kategorien; ohne Auswahl wird der Beitrag unter `undefined` einsortiert |
| **Zugriffsstufe** | ja | `Public`, `Registered` (nur angemeldet), `Premium` (aktive Berechtigung) |
| **Status** | ja | `Draft` (Standard), `Scheduled`, `Published`, `Archived` |
| **Geplante Veröffentlichung** | nein | Zeitpunkt (`ScheduledAt`); erforderlich bei `Status = Scheduled` — der Beitrag wird dann automatisch veröffentlicht |
| **Tags** | nein | kommagetrennt (auch `;` oder Zeilenumbruch); neue Tags werden automatisch angelegt |
| **Hashtags** | nein | kommagetrennt, mit oder ohne führendes `#`; eigene Struktur für Filter/Navigation |
| **Inhalt (Markdown)** | ja | Editor mit Live-Vorschau (EasyMDE); wird serverseitig gerendert und sanitized |
| **Video-URL** | nein | YouTube, Vimeo, X, TikTok — wird per oEmbed aufgelöst |
| **Bild hochladen** | nein | JPEG, PNG, WebP oder GIF; dient als Titelbild, wenn keine externe URL gesetzt ist |

Nach dem Absenden wird der Artikel angelegt und man landet zurück in der
Übersicht (Erfolgsmeldung via `TempData`).

### Details zu ausgewählten Feldern

- **Slug:** Wird der Slug leer gelassen, wird er aus dem Titel abgeleitet. Für
  Eindeutigkeit sorgt der `ArticleService`; existiert der Slug bereits, wird ein
  numerisches Suffix angehängt (`mein-artikel-2`).
- **Inhalt (Markdown):** Der Markdown-Text wird **nicht** roh ausgeliefert,
  sondern über Markdig gerendert und mit HtmlSanitizer bereinigt;
  `javascript:`-URLs werden entfernt (XSS-Schutz).
- **Video-URL:** Es wird ausschließlich der offizielle oEmbed-Code verwendet,
  eingebettet in ein `<iframe sandbox="allow-scripts allow-popups">` ohne
  `allow-same-origin`. Nicht unterstützte Plattformen werden nur verlinkt.
- **Bild:** Die Datei wird anhand des echten Inhalts validiert, skaliert,
  optional nach WebP konvertiert und **ohne EXIF** gespeichert. Im Dev-Modus
  liegt sie unter `wwwroot/uploads` (Provider `Local`).

## Markdown-Editor

Das Feld **Inhalt (Markdown)** wird mit dem Editor
[EasyMDE](https://github.com/Ionaru/easy-markdown-editor) ausgestattet. Er
funktioniert beim **Anlegen und Bearbeiten** gleichermaßen und lädt den
vorhandenen Inhalt automatisch.

- **Formatted Mode:** Über die Toolbar-Buttons *Vorschau* bzw. *Side-by-Side*
  wird der formatierte Inhalt neben dem Text angezeigt. Die Vorschau wird
  serverseitig mit demselben Renderer wie die öffentliche Seite erzeugt
  (Markdig + HtmlSanitizer) und entspricht damit exakt der späteren Ausgabe.
- **Bilder einfügen:** Der Bild-Button der Toolbar öffnet den Dateidialog,
  lädt das Bild hoch und fügt an der Cursorposition `![alt](url)` ein. Das Bild
  erscheint nach dem Speichern zusätzlich in der Bilderverwaltung und als
  Artikel-Galerie.
- **Bildergalerie (nur beim Anlegen):** Auf der Seite *Neue Akte* gibt es die
  Sektion **„Bilder"**. Dort werden alle in dieser Sitzung hochgeladenen Bilder
  als Thumbnails angezeigt. Ein Klick auf ein Thumbnail fügt das Bild an der
  aktuellen Cursorposition in den Text ein. Über **„Bild hochladen"** lassen
  sich weitere Bilder direkt ergänzen.
- **Video einfügen:** Der Video-Button fragt eine URL ab, trägt sie in das Feld
  **Video-URL** ein und setzt einen Verweis im Text. Das Video wird wie bisher
  unter dem Artikel eingebettet.
- **Ohne JavaScript:** Der Editor ist ein Progressive Enhancement. Ohne
  JavaScript bleibt das normale Textfeld samt Datei-Upload vollständig nutzbar.

> Hinweis: Bilder, die im Editor hochgeladen, aber vor dem Speichern verworfen
> werden, bleiben als unzugeordnete Medien-Assets liegen. Sie sind öffentlich
> nicht sichtbar und können später aufgeräumt werden.

## Artikeltags & Kategorien

- **Kategorien** werden im Admin-Bereich unter **Kategorien** gepflegt
  (anlegen, umbenennen, löschen). Jeder Beitrag verweist auf genau eine
  Kategorie; ohne Auswahl landet er unter der geschützten Systemkategorie
  `undefined`.
- Beim Löschen einer Kategorie werden ihre Beiträge automatisch zu
  `undefined` verschoben. `undefined` selbst kann weder gelöscht noch
  umbenannt werden.
- Ein optionaler **Disclaimer** pro Kategorie wird auf der Artikelseite
  eingeblendet.
- Jede Kategorie hat eine **Farbe** (`#RRGGBB`). Sie erscheint als **3 px
  dicker Rahmen** um jeden Beitragseintrag in Listen und macht die Kategorie
  auf einen Blick erkennbar. Die Farbe wird im Kategorien-Formular über den
  Farbwähler geändert; ohne Angabe gilt das neutrale Grau `#7f8c8d`.
- **Tags** sind frei und werden bei Bedarf neu erzeugt. Sie dienen als
  Filter auf der öffentlichen Artikelliste (`/Articles?tag={slug}`).

## Darstellung & Listenlänge

Unter **Einstellungen** (`/Admin/Settings`) legt der Administrator fest, wie
viele Beiträge gleichzeitig in einer Liste angezeigt werden. Der Wert gilt für
Startseite, Suche und das REST-Grid (`/api/articles`) und ist auf 1–50 begrenzt.

## Ursprung (Mensch / KI / Hybrid)

Im Formular unter **Einordnung & Themengebiet** legt der Autor den **Ursprung**
des Beitrags fest:

| Wert | Bedeutung | Icon in Listen |
|---|---|---|
| `Human` | Von einem Menschen erstellt (Standard) | Person |
| `Ai` | Mit künstlicher Intelligenz erstellt | Roboter |
| `Hybrid` | Mensch und KI gemeinsam | Person + Funke |

Das Icon erscheint in jeder Beitragskarte; beim Hover zeigt es den erklärenden
Text. Der Ursprung ist auch über die Management-API pflegbar (`origin`).

## Veröffentlichen (Publish-Workflow)

- Nur Artikel mit `Status = Published` **und** gesetztem `PublishedAt` sind
  öffentlich sichtbar.
- `PublishedAt` wird **einmalig** beim ersten Wechsel auf `Published` gesetzt
  und danach nicht mehr überschrieben.
- Ein Entwurf (`Draft`) ist also erst nach Umschalten auf `Published` im Blog
  sichtbar.
- **`Scheduled`** (geplante Veröffentlichung): Der Beitrag erhält einen
  `ScheduledAt`-Zeitpunkt und bleibt bis dahin unveröffentlicht. Ein
  Hintergrunddienst veröffentlicht ihn automatisch zum Zeitpunkt
  (`Status` → `Published`, `PublishedAt = ScheduledAt`).
- `Archived` nimmt einen Artikel aus der öffentlichen Liste, ohne ihn zu löschen.

Kurz: Anlegen/bearbeiten mit `Status = Published` → erscheint unter `/Articles`
und im RSS-Feed `/feed`.

## Linklisten (`/Admin/LinkLists`)

Nur für `Admin`. Hier werden redaktionelle, **geordnete** Listen von Beiträgen
angelegt und verwaltet:

- Zuordnung von Beiträgen zu einer Liste, inklusive Reihenfolge (unabhängig vom
  Veröffentlichungsdatum).
- Öffentliche Darstellung unter `/LinkLists/{slug}`; die Liste ist anklickbar und
  in andere Seiten einbettbar.

## Artikel bearbeiten (`/Admin/Articles/Edit?id=…`)

- Das Formular ist mit den aktuellen Werten vorbefüllt (inkl. Tags als
  kommagetrennte Liste und vorhandener Video-URL).
- Beim Speichern werden Slug, Inhalt, Teaser, Kategorie, Premium, Status und
  Tags aktualisiert.
- Wird eine Video-URL gesetzt, ersetzt sie vorhandene Video-Einbettungen des
  Artikels.
- Wird ein Bild hochgeladen, wird es als weiteres Medien-Asset ergänzt.

## Artikel löschen

- Über die Schaltfläche **Löschen** in der Übersicht (mit Sicherheitsabfrage).
- Das Löschen ist ein **Soft-Delete**: Der Artikel erhält einen `DeletedAt`-Zeitstempel
  und erscheint nicht mehr in der Verwaltung/öffentlich, bleibt aber in der
  Datenbank erhalten (Datenintegrität / Historie).

## Praktischer Ablauf (Kurzfassung)

```mermaid
flowchart TD
    A["Als Admin anmelden"] --> B["/Admin/Articles - Übersicht"]
    B --> C["Neuer Artikel"]
    C --> D["Felder ausfüllen: Titel, Kategorie, Markdown, Status"]
    D --> E["Tags / Premium / Video / Bild optional"]
    E --> F["Anlegen"]
    F --> G["Zurück zur Übersicht"]
    G --> H{"Status = Published?"}
    H -->|ja| I["Sichtbar unter /Articles und /feed"]
    H -->|nein| J["Draft - nur intern sichtbar; später bearbeiten & veröffentlichen"]
```

## Weiterführend

- Rollen & Admin-Zugang: [`01-rollen-und-admin.md`](01-rollen-und-admin.md)
- Blog aus Nutzersicht: [`03-blog-nutzen.md`](03-blog-nutzen.md)
