# Pflichtenheft — Blog-CMS

Dieses Verzeichnis enthält das Pflichtenheft für das selbst programmierte CMS (ASP.NET). Ausgangspunkt für alle Module ist `DataSchema.md` — **jede Entität, jedes Feld und jede Beziehung, die in den folgenden Dokumenten verwendet wird, muss dort definiert sein.** Widersprüche werden zugunsten von `DataSchema.md` aufgelöst.

## Struktur

| Datei | Inhalt |
|---|---|
| `FeatureFix1.MD` | **Fachliche Referenz des Blocks FeatureFix1** — generische, technologieunabhängige Blog-Regelsammlung (BR-010 … BR-121) |
| `feature_implementation1.md` | Soll-Ist-Abgleich FeatureFix1 ↔ Projekt, Gap-Analyse und Umsetzungsplan |
| `DataSchema.md` | Single Point of Truth — vollständiges Datenmodell |
| `00-Uebersicht.md` | Projektziel, Rahmenbedingungen, Tech-Stack |
| `01-Content-Verwaltung.md` | Artikel, Titelbild, Kategorien (Politik/Satire/Verschwörungstheorien), Tags, Hashtags, Zugriffsstufen, Linklisten, Bewertungen |
| `02-Kommentarfunktion.md` | Kommentare, Threads, Moderation, Meldungen, Auszeichnen, Bewertungen |
| `03-Medien-Upload-und-Embedding.md` | Titelbild, Bild-Uploads, Video-Verknüpfung (oEmbed) |
| `04-Monetarisierung.md` | Membership/Paywall, Zugriffsstufen, Stripe, Spenden, Newsletter |
| `05-Benutzerverwaltung-Auth.md` | Rollen (Reader/Author/Premium/Moderator/Admin), Auth, Rechte |
| `docs/` | Praxisnahe Anleitungen (Rollen/Admin, Admin-Bereich, Blog-Nutzung) — siehe [`docs/README.md`](docs/README.md) |
| `API_Funktion_Mentalversagen.md` | **Ausführliche Anleitung zur Management-API** — Zweck, Server-Konfiguration, Client-Tools, Skripte (Python/PowerShell), KI-Agenten (Hermes, MCP) |

## Arbeitsweise für Coding Agents

1. `FeatureFix1.MD` als fachliche Referenz lesen (was das Blog leisten soll).
2. `feature_implementation1.md` für den aktuellen Umsetzungsstand und die Planung lesen.
3. `DataSchema.md` lesen — es ist die Referenz für alle Entitätsnamen/Felder.
4. Das jeweilige Feature-Dokument lesen.
5. Bei Unklarheiten oder nötigen Schema-Änderungen: zuerst `DataSchema.md` anpassen, dann Feature-Dokument, dann Code.
6. **Neue Abhängigkeiten nur mit freier Lizenz einführen** — siehe „Paket- und Lizenzregeln" unten.

## Paket- und Lizenzregeln (verbindlich)

Im Projekt dürfen **ausschließlich frei lizenzierte Open-Source-Pakete** verwendet werden. Kommerzielle, proprietäre oder „source-available"-Lizenzen mit Nutzungsbeschränkungen sind **nicht zulässig** — dazu zählen u. a. Six Labors Split License (ImageSharp ab v3), SSPL, BUSL, Commons Clause, Elastic License, „Non-Commercial"-Lizenzen sowie alles, was ab einer Umsatz- oder Mitarbeitergrenze kostenpflichtig wird.

**Zulässige Lizenzen:** MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PostgreSQL License, Zlib, SIL OFL 1.1 (Schriften), MPL-2.0 (nur nach Einzelfallprüfung).

**Verfahren bei jeder neuen Abhängigkeit (NuGet oder LibMan):**

1. Lizenz des Pakets **und** seiner transitiven Abhängigkeiten prüfen.
2. Lizenz in [`docs/06-lizenzen-und-pakete.md`](docs/06-lizenzen-und-pakete.md) eintragen.
3. Ist die Lizenz unklar oder nicht frei: Paket **nicht** einführen, stattdessen eine freie Alternative wählen.
4. Prüfbefehle ausführen:
   - `dotnet list src/BlogCms.slnx package --include-transitive`
   - `dotnet list src/BlogCms.slnx package --vulnerable --include-transitive`
   - Client-Bibliotheken: [`src/BlogCms.Web/libman.json`](src/BlogCms.Web/libman.json) — Lizenz je Bibliothek prüfen.

**Bestandsprüfung (2026-10-18):** Alle direkten und transitiven NuGet-Pakete sowie alle LibMan-Bibliotheken sind frei lizenziert (MIT, Apache-2.0, BSD-2-Clause, PostgreSQL License, SIL OFL 1.1). Es sind **keine proprietären Pakete** im Projekt. Die kommerzialisierte Bibliothek `SixLabors.ImageSharp` wurde durch `Magick.NET-Q8-AnyCPU` (Apache-2.0) ersetzt — siehe [`plans/imagesharp-ersetzen.md`](plans/imagesharp-ersetzen.md).
