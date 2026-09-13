# 01 — Content-Verwaltung

Referenzierte Entitäten aus `DataSchema.md`: `Article`, `Tag`, `ArticleTag`, `Hashtag`,
`ArticleHashtag`, `Rating`, `LinkList`, `LinkListItem`.

Fachliche Referenz für diesen Block: [`FeatureFix1.MD`](FeatureFix1.MD) (Regeln BR-020 bis
BR-042, BR-070, BR-080 bis BR-092). Der Soll-Ist-Abgleich liegt in
[`feature_implementation1.md`](feature_implementation1.md).

## Funktionale Anforderungen

### Artikel erstellen/bearbeiten
- `Author` (Autor) legt **eigene** Artikel an und bearbeitet sie; `Admin` darf alle Artikel
  verwalten (BR-013/BR-014/BR-033).
- Pflichtfelder: `Title`, `TitleImageUrl` **oder** hochgeladenes Titelbild, `Excerpt`
  (Kurzbeschreibung), `ContentMarkdown`, `Category`, `AccessLevel` (BR-021/022/023/024/110).
- Editor: Markdown-Eingabe im Backend (clientseitige Markdown-Editor-Komponente),
  serverseitiges Rendering zu HTML beim Ausliefern (Sanitizing verpflichtend).
- **Besitzregel:** `Author` darf keine fremden Beiträge bearbeiten/verwalten; `Admin` ist
  nicht eingeschränkt.

### Titelbild (BR-022)
- Ein Beitrag besitzt ein Titelbild. Es kann stammen aus:
  1. einer externen URL (`TitleImageUrl`) oder
  2. einem vom Blog verwalteten Bild (`MediaAsset`; erstes Artikel-Medien-Asset als Fallback).
- Das Titelbild wird in Übersichten, Vorschauen und der Einzelansicht verwendet.

### Kurzbeschreibung (BR-023)
- Jeder Beitrag besitzt eine Kurzbeschreibung (`Excerpt`), unabhängig vom Beitragstext.
- Sie dient der Darstellung in Übersichten, Suchergebnissen und RSS-Teaser.

### Kategorisierung
- Jeder Artikel erhält genau eine `Category`: `Politik`, `Satire` oder `Verschwoerungstheorien`.
  (FeatureFix1 erlaubt mehrere Kategorien — bewusste Abweichung, siehe Abgleich.)
- Zusätzlich frei vergebbare `Tag`s (n:m über `ArticleTag`) und `Hashtag`s (n:m über
  `ArticleHashtag`) für granulare Verschlagwortung/Filterung (BR-026).
- Die `Category` wird auf der Artikelseite sichtbar angezeigt (Badge); bei `Satire` und
  `Verschwoerungstheorien` wird ein Hinweistext/Disclaimer eingeblendet.

### Status & Workflow
- Status-Übergänge: `Draft` → `Scheduled` → `Published` → `Archived` (BR-030/031/032).
- Nur `Published`-Artikel sind öffentlich sichtbar.
- `PublishedAt` wird beim Übergang zu `Published` gesetzt (nicht überschrieben bei späteren Edits).
- **Geplante Veröffentlichung:** setzt der Autor `Status = Scheduled` und `ScheduledAt` in der
  Zukunft, bleibt der Beitrag unveröffentlicht und wird **automatisch** zum Zeitpunkt
  veröffentlicht (Hintergrunddienst `ArticleSchedulerService`).
- `Author` entscheidet selbst über Entwurf/Veröffentlichung; keine Admin-Freigabe nötig.

### Zugriffsstufen (BR-110/111/112/113)
- `AccessLevel`: `Public` (öffentlich), `Registered` (nur angemeldete Benutzer),
  `Premium` (aktive Berechtigung/Abo).
- Premium-/geschützte Beiträge erscheinen öffentlich als **Teaser** (Titel, Titelbild,
  Kurzbeschreibung, Datum, ggf. Autor, Kennzeichnung). Der eigentliche Inhalt wird nicht
  vollständig angezeigt.
- Nicht berechtigte Besucher erhalten einen CTA: bei `Registered` → Anmelden, bei `Premium`
  → Mitgliedschaft erwerben (`/Membership`).

### Bewertungen (BR-070)
- Angemeldete Benutzer können Beiträge mit 👍/👎 bewerten (max. eine Bewertung gleichzeitig,
  änderbar). Anonyme Besucher dürfen nicht bewerten.
- Die Bewertungssumme ist als Filter-/Sortierkriterium nutzbar (siehe unten und
  `02-Kommentarfunktion.md` für Kommentarbewertungen).

### Listing, Suche & Filterung (BR-080/081/082)
- Startseite/Übersicht: paginierte Liste `Published`-Artikel, neueste zuerst.
- Vorschauinformationen: Titelbild, Titel, Kurzbeschreibung, Autor, Veröffentlichungs- und
  Änderungsdatum, Kategorien, Tags, Hashtags, Kennzeichnung der Zugriffsstufe, Bewertung.
- Such- und Filterkriterien: Suchbegriff, Kategorie, Tag, Hashtag, Autor,
  Veröffentlichungs-/Änderungsdatum, Bewertung, Zugriffsstufe — inklusive Sortierung.
- RSS-Feed über alle `Published`-Artikel (BR-100).

### Linklisten (BR-090/091/092)
- Der `Admin` erstellt und verwaltet benutzerdefinierte, **geordnete** Linklisten
  (`LinkList`/`LinkListItem`). Die Reihenfolge (`Position`) ist unabhängig vom
  Veröffentlichungsdatum.
- Eine Linkliste ist öffentlich darstellbar (`/LinkLists/{slug}`) und in andere Seiten
  einbettbar (anklickbare Verweise auf die Beiträge).

## Akzeptanzkriterien

- [ ] Artikel ohne `Category` können nicht veröffentlicht werden (Validierung).
- [ ] Artikel ohne Titelbild (externe URL oder Upload) können nicht veröffentlicht werden.
- [ ] Artikel ohne Kurzbeschreibung (`Excerpt`) können nicht veröffentlicht werden.
- [ ] Slug ist eindeutig; eine bereits veröffentlichte URL bleibt bei Titeländerung stabil.
- [ ] Markdown-Inhalt wird beim Rendering serverseitig sanitized (kein Roh-HTML/Script-Injection).
- [ ] `Author` kann ausschließlich eigene Beiträge bearbeiten; `Admin` alle.
- [ ] `Scheduled`-Beiträge werden zum `ScheduledAt`-Zeitpunkt automatisch veröffentlicht.
- [ ] Geschützte/Premium-Artikel zeigen für nicht berechtigte Leser nur den Teaser + passenden CTA.
- [ ] Angemeldete Nutzer können Beiträge bewerten (änderbar, max. 1 Wert); Unangemeldete nicht.
- [ ] Beiträge sind über Suchbegriff, Kategorie, Tag, Hashtag, Autor, Datum, Bewertung und
      Zugriffsstufe filter- und sortierbar.
- [ ] Admin kann Linklisten anlegen, ordnen und öffentlich/eingebettet darstellen.
- [ ] RSS-Feed validiert gegen den RSS-2.0-Standard und enthält das Titelbild.
