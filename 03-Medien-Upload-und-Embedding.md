# 03 — Medien-Upload und Video-Embedding

Referenzierte Entitäten aus `DataSchema.md`: `MediaAsset`, `VideoEmbed`.

## Teil A — Bild-Upload

### Funktionale Anforderungen
- Upload für Artikel (im Redaktions-Backend) und für Kommentare (im Frontend, für angemeldete Nutzer).
- Erlaubte Formate: JPEG, PNG, WebP, GIF. Serverseitige Validierung anhand des tatsächlichen Datei-Inhalts (nicht nur Dateiendung/MIME-Header des Clients).
- Maximale Dateigröße: konfigurierbar, empfohlen unterschiedliche Limits für Artikel- vs. Kommentar-Uploads (Kommentare enger begrenzt, z. B. 5 MB).
- Nach Upload: serverseitige Verarbeitung
  - Resize auf maximale Kantenlänge (verhindert exzessive Auflösungen)
  - Optionale Konvertierung nach WebP zur Reduktion der Dateigröße
  - Entfernen von EXIF-Metadaten (Datenschutz — verhindert versehentliche Preisgabe von Standortdaten etc.)
- Speicherung im Objekt-Storage (S3-kompatibel / Blob Storage), nicht im Anwendungsserver-Dateisystem. `MediaAsset.StoragePath` referenziert den Storage-Key.
- Ausgelieferte URLs werden über eine CDN-Schicht oder signierte URLs bereitgestellt (abhängig vom gewählten Storage-Anbieter).

### Moderation von Kommentar-Bildern
- Bild-Anhänge an Kommentaren folgen demselben Moderationsstatus wie der zugehörige `Comment` (kein separater Freigabeprozess nötig, aber technisch trennbar für spätere Erweiterung, z. B. automatisierte Bilderkennung).

## Teil B — Video-Verknüpfung (oEmbed)

### Funktionale Anforderungen
- Nutzer/Redakteur fügt eine Video-URL ein (YouTube, Vimeo, X/Twitter, TikTok, ggf. weitere).
- Backend erkennt die Plattform anhand der URL-Struktur (Pattern-Matching) und setzt `VideoEmbed.Platform` entsprechend; unbekannte Muster werden als `Other` markiert und nicht automatisch eingebettet, sondern nur als Link dargestellt.
- Backend ruft den passenden **oEmbed-Endpunkt** der Plattform auf, speichert den zurückgegebenen `EmbedHtml` sowie `ThumbnailUrl` in `VideoEmbed`.
- Der Abruf erfolgt serverseitig und wird zwischengespeichert (nicht bei jedem Seitenaufruf erneut) — bei Bedarf lässt sich der Cache erneuern (z. B. wenn eine Plattform ihr Embed-Format ändert).

### Sicherheit
- `EmbedHtml` wird beim Rendern in ein **sandboxed `<iframe>`** eingebettet (`sandbox`-Attribut ohne `allow-same-origin` in Kombination mit `allow-scripts`, je nach Plattform-Anforderung), um XSS-Risiken über manipulierte Embed-Inhalte zu minimieren — insbesondere relevant, da Kommentare nutzergeneriert sind.
- Es wird ausschließlich der über oEmbed offiziell gelieferte Code verwendet, kein Parsen/Konstruieren eigener Embed-iframes aus der Roh-URL.

## Akzeptanzkriterien

- [ ] Upload einer Datei mit falscher Endung, aber echtem Bildinhalt, wird korrekt anhand des Dateiinhalts validiert.
- [ ] EXIF-Daten sind nach Verarbeitung aus dem gespeicherten Bild entfernt.
- [ ] Eine eingefügte Video-URL einer nicht unterstützten Plattform wird als einfacher Link angezeigt, nicht als eingebettetes iframe.
- [ ] Eingebettete Videos laufen in einem sandboxed iframe, verifizierbar im gerenderten HTML.
- [ ] Kommentar-Bild-Uploads sind auf ein kleineres Limit begrenzt als Artikel-Uploads.
