# Plan: Dokumentation aktualisieren, Administration und Autoren-Handbuch

## 1. Ziel

1. Die vorhandene Dokumentation mit dem tatsächlichen Projektstand abgleichen.
2. Veraltete Aussagen löschen und obsolete Bestandteile restlos entfernen.
3. Eine **Administrations-Dokumentation** erstellen.
4. Ein **Autoren-Handbuch** für die Systembedienung erstellen.

---

## 2. Bestandsaufnahme: veraltete Stellen

| Datei | Veraltete Aussage | Tatsächlicher Stand |
|---|---|---|
| [`00-Uebersicht.md`](00-Uebersicht.md) | Kategorien seien das Enum `Politik`/`Satire`/`Verschwoerungstheorien` und Pflichtfeld | Kategorien sind dynamisch, optional, Default `undefined` |
| [`00-Uebersicht.md`](00-Uebersicht.md) | Zahlungen: Stripe + optional PayPal | Nur Stripe; Spenden-Backend ohne UI |
| [`00-Uebersicht.md`](00-Uebersicht.md) | Medien: S3/Azure Blob | `LocalDiskStorageService` (Default) oder S3 |
| [`00-Uebersicht.md`](00-Uebersicht.md) | Newsletter über Transaktionsmail-API | SMTP (`SmtpEmailSender`) bzw. Dev-Datei |
| [`01-Content-Verwaltung.md`](01-Content-Verwaltung.md) | `Category` sei Pflichtfeld | Kategorie ist optional (`undefined`) |
| [`01-Content-Verwaltung.md`](01-Content-Verwaltung.md) | Kein Ursprung, keine Kategorie-Farbe | `Article.Origin`, `Category.Color` fehlen |
| [`02-Kommentarfunktion.md`](02-Kommentarfunktion.md) | `VideoEmbed` für Kommentare | Kommentar-Videos nicht implementiert |
| [`03-Medien-Upload-und-Embedding.md`](03-Medien-Upload-und-Embedding.md) | Speicherung nur im Objekt-Storage | Local Disk ist Default, S3 optional |
| [`03-Medien-Upload-und-Embedding.md`](03-Medien-Upload-und-Embedding.md) | CDN/signierte URLs | Nicht implementiert |
| [`04-Monetarisierung.md`](04-Monetarisierung.md) | Newsletter-Versand über API-Anbieter | SMTP |
| [`haiku_develop.md`](haiku_develop.md) | `Category` sei ein Enum (`ArticleCategory`) | Dynamische `Category`-Tabelle |
| [`API_Funktion_Mentalversagen.md`](API_Funktion_Mentalversagen.md) | Nur `/api/v1/articles` | Zusätzlich `/api/v1/categories`, `/api/v1/hashtags`, Hashtag-Endpunkte |
| [`API_Funktion_Mentalversagen.md`](API_Funktion_Mentalversagen.md) | Kein `origin`, kein `color` | Beide Felder existieren |
| [`feature_implementation1.md`](feature_implementation1.md) | Veraltete Entitäten/Enums (`Subscription`, `ArticleCategory`, `SubscriptionStatus`) | Historisch; muss bereinigt werden |
| [`docs/01-rollen-und-admin.md`](docs/01-rollen-und-admin.md) | „es gibt keine Benutzerverwaltungs-UI“ | `/Admin/Users` existiert |
| [`docs/04-mailversand.md`](docs/04-mailversand.md) | Status „Vorgabe“, SMTP fehle | SMTP ist implementiert |
| [`docs/10-abnahme-mitgliedschaft.md`](docs/10-abnahme-mitgliedschaft.md) | „271 erfolgreich“ | Aktuell 301 Tests |
| [`DataSchema.md`](DataSchema.md) | Änderungsprotokoll endet bei v1.5 | v1.6 (Farbe/SiteSetting) und v1.7 (Origin) fehlen |

---

## 3. Aufgabe A — Vorhandene Doku aktualisieren

### 3.1 Root-Dokumente

- **`00-Uebersicht.md`**
  - Kategorien: dynamisch, optional, `undefined` als Default.
  - Zahlungen: Stripe-Mitgliedschaft; Spenden-Backend vorhanden, UI Platzhalter.
  - Medien: Local Disk (Default) oder S3.
  - Newsletter: SMTP bzw. Dev-Datei.
  - Tech-Stack an den tatsächlichen Stand anpassen (Razor Pages + MVC, PostgreSQL, Identity, Stripe.net, MailKit, Magick.NET).
- **`01-Content-Verwaltung.md`**
  - Kategorie optional; `undefined` als Fallback.
  - `Article.Origin` (Mensch/KI/Hybrid) ergänzen.
  - `Category.Color` und 3-px-Rahmen ergänzen.
  - Akzeptanzkriterien anpassen (Kategorie nicht mehr Pflicht).
- **`02-Kommentarfunktion.md`**
  - Kommentar-Video-Referenzen entfernen, falls nicht implementiert.
  - Thread-Tiefe und tatsächliche UI-Funktionen präzisieren.
- **`03-Medien-Upload-und-Embedding.md`**
  - Speicherung: Local Disk (Default) / S3 (optional).
  - CDN/signierte URLs als „nicht implementiert“ kennzeichnen oder entfernen.
  - WebP-Konvertierung und Limits an `MediaOptions` angleichen.
- **`04-Monetarisierung.md`**
  - Newsletter-Versand auf SMTP korrigieren.
  - Spenden-UI als Platzhalter bestätigen.
- **`05-Benutzerverwaltung-Auth.md`**
  - Rollenverwaltung über `/Admin/Users` ergänzen.
  - `Premium`-Rolle als optional/abgeleitet präzisieren.
- **`haiku_develop.md`**
  - Kategorien-Aussage korrigieren (Tabelle, nicht Enum).
  - Offene Punkte prüfen und aktualisieren.
- **`API_Funktion_Mentalversagen.md`**
  - Endpunkt-Tabelle um Kategorien und Hashtags erweitern.
  - `origin` und `color` in den Feldtabellen ergänzen.
  - „Alle Endpunkte unter `/api/v1/articles`“ korrigieren.
- **`feature_implementation1.md`**
  - Als historisches Dokument kennzeichnen (Hinweis existiert bereits).
  - Veraltete Entitäts-/Enum-Listen entfernen oder auf den aktuellen Stand bringen.
- **`DataSchema.md`**
  - Änderungsprotokoll um v1.6 (Kategorie-Farbe, `SiteSetting`) und v1.7 (`ArticleOrigin`) ergänzen.
- **`RUNNING.md`**
  - Admin-Seiten (`/Admin/Settings`, `/Admin/Categories`, `/Admin/Memberships`) und neue Konfiguration prüfen/ergänzen.
- **`README.md`**
  - Struktur um die neuen Doku-Dateien ergänzen.
  - Verweise auf `haiku_develop.md` und `API_Funktion_Mentalversagen.md` prüfen.

### 3.2 `docs/`-Dokumente

- **`docs/01-rollen-und-admin.md`**
  - Primärer Weg: `/Admin/Users` (Rollenwechsel).
  - SQL nur noch als Fallback.
  - Satz „keine Benutzerverwaltungs-UI“ entfernen.
- **`docs/04-mailversand.md`**
  - Status auf „umgesetzt“ setzen.
  - Ausgangslage/Fehlerbeschreibung als historischen Kontext kennzeichnen.
  - Tatsächliche Implementierung (`SmtpEmailSender`, `EmailOptions`, `Provider`) dokumentieren.
- **`docs/10-abnahme-mitgliedschaft.md`**
  - Testzahl auf 301 aktualisieren.
- **`docs/README.md`**
  - Neue Dateien `12-administration.md` und `13-autoren-handbuch.md` verlinken.
  - Beschreibungen von `02` und `03` präzisieren.

---

## 4. Aufgabe B — Administrations-Dokumentation

Neue Datei `docs/12-administration.md` mit folgenden Abschnitten:

1. **Zweck und Zielgruppe** — Betreiber/Administratoren.
2. **Admin-Zugang** — Konto, E-Mail-Bestätigung, Rolle über `/Admin/Users` oder SQL.
3. **Verwaltungsübersicht** — Navigation und erreichbare Seiten.
4. **Artikelverwaltung** — `/Admin/Articles`: Filter, Suche, Statuswechsel, Kategorie ändern, Löschen.
5. **Kategorien** — `/Admin/Categories`: anlegen, bearbeiten, Farbe, Disclaimer, Reihenfolge, `undefined`-Schutz, Artikel-Umzug beim Löschen.
6. **Linklisten** — `/Admin/LinkLists`: anlegen, bearbeiten, Positionen, öffentliche Darstellung und Embed.
7. **Benutzer & Rollen** — `/Admin/Users`: Rollenwechsel, Synchronisation mit Identity.
8. **Mitgliedschaften** — `/Admin/Memberships`: Übersicht, Filter, Freigaben, Sync, Audit-Log, Voraussetzung `Membership:Enabled`.
9. **Einstellungen** — `/Admin/Settings`: Beiträge pro Liste.
10. **Aktivitätsauswertung** — `/Admin/Activity`: Zeitraum, Aufrufe, Bewertungen, Kommentare, Verlauf.
11. **Moderation** — `/Moderation`: Meldungen, Freigeben/Ablehnen, Verwerfen.
12. **Betrieb** — Feature-Flags, API-Key, Stripe, SMTP, Medien, Datenbank/Migrationen, Backup-Hinweise.
13. **Sicherheit** — serverseitige Policies, Fail-Closed-API, Secrets nicht im Repo.
14. **Troubleshooting** — häufige Probleme und Lösungen.

---

## 5. Aufgabe C — Autoren-Handbuch

Neue Datei `docs/13-autoren-handbuch.md` mit folgenden Abschnitten:

1. **Zweck und Zielgruppe** — Autorinnen und Autoren.
2. **Anmeldung und Voraussetzungen** — Konto, E-Mail-Bestätigung, Rolle `Author`.
3. **Einstieg** — `/Admin/Articles`, eigene Beiträge, Filter/Suche.
4. **Neuen Beitrag anlegen** — `/Admin/Articles/Create`.
5. **Formularfelder im Detail**:
   - Titel, Slug, Kurzbeschreibung
   - Kategorie
   - Zugriffsstufe (`Public`, `Registered`, `Premium`)
   - Ursprung (`Human`, `Ai`, `Hybrid`)
   - Status (`Draft`, `Scheduled`, `Published`, `Archived`)
   - geplante Veröffentlichung
   - Tags und Hashtags
   - Inhalt (Markdown)
   - Titelbild (URL, Upload, Galerie)
   - Video-URL
6. **Markdown-Editor** — Vorschau, Bilder einfügen, Formatierung.
7. **Bilder und Medien** — Upload, Galerie, Titelbild, Alt-Texte, Grenzen.
8. **Video einbetten** — unterstützte Plattformen, sichere Einbettung.
9. **Speichern, Veröffentlichen, Planen** — Buttons und Regeln.
10. **Beitrag bearbeiten** — `/Admin/Articles/Edit`, Besitzregel, Slug-Stabilität.
11. **Beitrag löschen** — Soft-Delete, Wirkung.
12. **Vorschau** — Entwürfe/geplante Beiträge.
13. **Kommentare der eigenen Beiträge moderieren** — löschen, auszeichnen.
14. **Checkliste vor Veröffentlichung**.
15. **Häufige Fehler** — fehlendes Titelbild, fehlende Kurzbeschreibung, ungültiger Slug, geplante Zeit in der Vergangenheit.

---

## 6. Aufräumen

- Veraltete Absätze in den oben genannten Dateien **ersetzen**, nicht nur ergänzen.
- Widersprüchliche Aussagen (Enum vs. Tabelle, PayPal, Azure Blob, API-Mailversand) entfernen.
- `feature_implementation1.md` bleibt als historisches Dokument, aber ohne veraltete Entitätslisten.
- `plans/` bleiben historische Arbeitsdokumente; sie werden nicht als aktuelle Doku verlinkt.
- `docs/README.md` und `README.md` erhalten eine klare Trennung:
  - Root: Pflichtenheft/Feature-Spezifikationen
  - `docs/`: praktische Anleitungen (Admin, Autor, Betrieb)

---

## 7. Umsetzungsreihenfolge

1. Root-Dokumente aktualisieren (`00`–`05`, `haiku_develop`, `API_Funktion`, `feature_implementation1`, `DataSchema`, `RUNNING`, `README`).
2. `docs/01`, `docs/04`, `docs/10` korrigieren.
3. `docs/12-administration.md` erstellen.
4. `docs/13-autoren-handbuch.md` erstellen.
5. `docs/README.md` und `README.md` verlinken.
6. Abschluss: Suche nach veralteten Begriffen (`ArticleCategory`, `PayPal`, `Azure Blob`, „keine Benutzerverwaltungs-UI“, „Subscription-Tabelle“ als aktuelle Aussage) und bereinigen.

---

## 8. Abnahmekriterien

- Keine aktuelle Doku-Aussage widerspricht dem Code.
- Kategorien werden überall als dynamisch und optional beschrieben.
- `Article.Origin`, `Category.Color` und `SiteSetting` sind dokumentiert.
- Admin-Doku deckt alle Admin-Seiten ab.
- Autoren-Handbuch deckt den vollständigen Artikel-Workflow ab.
- `docs/README.md` verlinkt beide neuen Dokumente.
- Veraltete Begriffe kommen nur noch in historischen Dokumenten (`plans/`, `feature_implementation1.md` mit Hinweis) vor.
