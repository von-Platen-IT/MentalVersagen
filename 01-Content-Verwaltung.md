# 01 — Content-Verwaltung

Referenzierte Entitäten aus `DataSchema.md`: `Article`, `Tag`, `ArticleTag`.

## Funktionale Anforderungen

### Artikel erstellen/bearbeiten
- Autor (Rolle `Admin` oder zukünftig weitere Redakteurs-Rollen) kann Artikel im Backend anlegen und bearbeiten.
- Pflichtfelder: `Title`, `Slug` (automatisch aus Titel generierbar, manuell überschreibbar), `ContentMarkdown`, `Category`.
- Editor: Markdown-Eingabe im Backend (z. B. mittels clientseitiger Markdown-Editor-Komponente), serverseitiges Rendering zu HTML beim Ausliefern (Sanitizing verpflichtend, siehe Sicherheitsanforderungen unten).

### Kategorisierung
- Jeder Artikel erhält genau eine `Category`: `Politik`, `Satire` oder `Verschwoerungstheorien`.
- Zusätzlich frei vergebbare `Tag`s (n:m über `ArticleTag`) für granulare Verschlagwortung/Filterung.
- Die `Category` wird auf der Artikelseite sichtbar angezeigt (z. B. als Badge) — bei `Satire` und `Verschwoerungstheorien` wird ein Hinweistext/Disclaimer eingeblendet (Formulierung liegt beim Nutzer, technisch: Template-Baustein je Kategorie).

### Status & Workflow
- Status-Übergänge: `Draft` → `Published` → `Archived`.
- Nur `Published`-Artikel sind öffentlich sichtbar.
- `PublishedAt` wird beim Übergang zu `Published` gesetzt (nicht überschrieben bei späteren Edits).

### Paywall
- `IsPremium`-Flag pro Artikel.
- Ist `IsPremium = true` und der Leser hat keinen aktiven `Subscription`-Status (siehe `04-Monetarisierung.md`), wird nur `Excerpt` angezeigt plus Call-to-Action zum Abschluss einer Mitgliedschaft.

### Listing & Filterung
- Startseite/Übersicht: paginierte Liste `Published`-Artikel, neueste zuerst.
- Filterbar nach `Category` und `Tag`.
- RSS-Feed über alle `Published`-Artikel (bzw. optional pro Kategorie).

## Akzeptanzkriterien

- [ ] Artikel ohne `Category` können nicht veröffentlicht werden (Validierung).
- [ ] Slug ist eindeutig; Kollision wird beim Speichern verhindert/gemeldet.
- [ ] Markdown-Inhalt wird beim Rendering serverseitig sanitized (kein Roh-HTML/Script-Injection möglich).
- [ ] Premium-Artikel zeigen für nicht-berechtigte Leser nur den Excerpt.
- [ ] RSS-Feed validiert gegen den RSS-2.0-Standard.
