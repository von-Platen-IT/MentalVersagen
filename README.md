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

## Arbeitsweise für Coding Agents

1. `FeatureFix1.MD` als fachliche Referenz lesen (was das Blog leisten soll).
2. `feature_implementation1.md` für den aktuellen Umsetzungsstand und die Planung lesen.
3. `DataSchema.md` lesen — es ist die Referenz für alle Entitätsnamen/Felder.
4. Das jeweilige Feature-Dokument lesen.
5. Bei Unklarheiten oder nötigen Schema-Änderungen: zuerst `DataSchema.md` anpassen, dann Feature-Dokument, dann Code.
