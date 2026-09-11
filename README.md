# Pflichtenheft — Blog-CMS

Dieses Verzeichnis enthält das Pflichtenheft für das selbst programmierte CMS (ASP.NET). Ausgangspunkt für alle Module ist `DataSchema.md` — **jede Entität, jedes Feld und jede Beziehung, die in den folgenden Dokumenten verwendet wird, muss dort definiert sein.** Widersprüche werden zugunsten von `DataSchema.md` aufgelöst.

## Struktur

| Datei | Inhalt |
|---|---|
| `DataSchema.md` | Single Point of Truth — vollständiges Datenmodell |
| `00-Uebersicht.md` | Projektziel, Rahmenbedingungen, Tech-Stack |
| `01-Content-Verwaltung.md` | Artikel, Kategorien (Politik/Satire/Verschwörungstheorien), Tags |
| `02-Kommentarfunktion.md` | Kommentare, Threads, Moderation, Meldungen |
| `03-Medien-Upload-und-Embedding.md` | Bild-Uploads, Video-Verknüpfung (oEmbed) |
| `04-Monetarisierung.md` | Membership/Paywall, Stripe, Spenden, Newsletter |
| `05-Benutzerverwaltung-Auth.md` | Rollen, Auth, Rechte |
| `docs/` | Praxisnahe Anleitungen (Rollen/Admin, Admin-Bereich, Blog-Nutzung) — siehe [`docs/README.md`](docs/README.md) |

## Arbeitsweise für Coding Agents

1. `DataSchema.md` lesen — es ist die Referenz für alle Entitätsnamen/Felder.
2. Das jeweilige Feature-Dokument lesen.
3. Bei Unklarheiten oder nötigen Schema-Änderungen: zuerst `DataSchema.md` anpassen, dann Feature-Dokument, dann Code.
