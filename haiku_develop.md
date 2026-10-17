# haiku_develop.md — Arbeitsregeln für MentalVersagen (BlogCms)

Diese Datei fasst die Erkenntnisse aus der Backend-Analyse zusammen. Sie ist die Grundlage für jede Änderung am Projekt.

---

## 1. Projektüberblick

- **Stack:** .NET 10, ASP.NET Core Razor Pages + MVC-Controller, EF Core, PostgreSQL (Dev/Prod), InMemory-Provider in Tests.
- **Schichten:**
  - `src/BlogCms.Domain` — Entitäten und Enums (keine Infrastruktur).
  - `src/BlogCms.Infrastructure` — Services, `AppDbContext`, EF-Configurations, Migrationen.
  - `src/BlogCms.Web` — Razor Pages, API-Controller, `Program.cs` (DI, Auth, Seeding).
  - `tests/BlogCms.Tests` — xUnit.
- **Verbindliche Quellen (Reihenfolge):**
  1. `DataSchema.md` — Single Point of Truth für das Datenmodell.
  2. `FeatureFix1.MD` — fachliche Regeln (BR-xxx).
  3. `00-Uebersicht.md` bis `05-Benutzerverwaltung-Auth.md` — Modul-Vorgaben.
  4. `feature_implementation1.md` — Soll-Ist-Abgleich (historisch).

## 2. Befehle

| Zweck | Befehl |
|---|---|
| Build + Tests | `dotnet test src/BlogCms.slnx` |
| Migration anlegen | `dotnet ef migrations add <Name> --project src/BlogCms.Infrastructure --startup-project src/BlogCms.Web` |
| Migration anwenden | `dotnet ef database update --project src/BlogCms.Infrastructure --startup-project src/BlogCms.Web` (Docker-Postgres) |

Nach jeder Änderung: Build und Tests ausführen, bevor eine Aufgabe als erledigt gilt.

## 3. Arbeitsablauf bei Datenmodell-Änderungen

1. Zuerst `DataSchema.md` ändern und das Änderungsprotokoll fortschreiben (Version erhöhen).
2. Entität/Enum in `BlogCms.Domain` anpassen.
3. `AppDbContext` bzw. Configuration-Klasse anpassen.
4. Migration erzeugen. Der Scaffolder warnt bei Datenverlust, daher Migration immer prüfen.
5. Tests anpassen und ergänzen.

## 4. Architekturregeln

- **Keine Geschäftslogik in PageModels.** Präsentation mappt Formulardaten auf Request-Objekte (z. B. `ArticleInputModel.ToRequest()` → `ArticlePostRequest`) und ruft Services auf.
- **Services geben Ergebnisse zurück, statt zu werfen:** `PostResult`, `CommentResult`, `RatingResult`, `MediaUploadResult`. Fehler werden feldbezogen an `ModelState` übergeben.
- **Autorisierung serverseitig prüfen.** Ownership (Autor), Rolle (Moderator/Admin) und Paywall (`CanReadContent`) gehören in Handler bzw. Services. UI-Ausblendung allein reicht nicht.
- **Infrastructure bleibt web-frei.** Kein `IFormFile` in Infrastructure; stattdessen `ArticleUpload(Stream, FileName)`.
- **Ressourcen freigeben.** Upload-Streams immer disposen (`ArticleUpload` implementiert `IAsyncDisposable`).

## 5. Datenmodell-Konventionen

- Primärschlüssel: `Guid`. Zeitstempel: UTC.
- Enums werden als String gespeichert (`EnumToStringConverter`, nur bei relationalen Providern aktiv).
- Soft-Delete über `DeletedAt`. `Article` hat einen globalen Query-Filter (`DeletedAt == null`). `Comment` bewusst ohne Filter, damit Antworten erhalten bleiben.
- `Category` ist **ein Enum** (`ArticleCategory`), keine Tabelle.
- Titelbild-Auflösung (`ArticleDisplay.ResolveTitleImage`): `TitleImageUrl` → Asset mit `IsCover` → ältestes Asset.
- `MediaAsset.IsCover` setzt man ausschließlich über `IMediaService.SetCoverAsync`, damit nur ein Cover pro Artikel existiert.
- Editor-Uploads ohne Artikel-Bezug (`ArticleId == null`) werden nach 24 h durch `ArticleSchedulerService` bereinigt.

## 6. Stolperfallen aus der Analyse

- **InMemory vs. relational:** InMemory unterstützt keine Transaktionen (Guard über `IsRelational()`). Required-Includes verwerfen Zeilen ohne Principal. Tests deshalb mit echten `User`-Datensätzen arbeiten lassen.
- **Datumswerte aus Formularen:** `datetime-local` liefert `DateTimeKind.Unspecified`. Vor Vergleich und Speicherung mit `ArticleValidator.NormalizeScheduledAtToUtc` nach UTC wandeln.
- **POST-Handler laden ihren Kontext selbst.** Eigenschaften wie `Article`, die nur im GET gesetzt werden, sind im POST `null`. Dadurch war die Autoren-Löschberechtigung defekt. Berechtigungen deshalb im Service prüfen.
- **Moderation:** Wird ein Kommentar freigegeben oder abgelehnt, werden offene Meldungen auf `Reviewed` gesetzt.
- **Wortfilter (Blocklist):** gilt beim Anlegen **und** Bearbeiten von Kommentaren.
- **Paywall:** Kommentieren und Bewerten setzen `CanReadContent` voraus. Autor und Admin haben immer Lesezugriff.
- **Vorschau:** Entwürfe, geplante und archivierte Artikel sind nur für Autor und Admin sichtbar. Vorschau-Aufrufe werden nicht im Aktivitätslog gezählt.
- **Sortierung „Beliebteste“:** Netto-Score (`ThumbUp - ThumbDown`), nicht nur Anzahl Likes.
- **Redirects nach Kommentar-/Bewertungsaktionen** verwenden URL-Fragmente (`#comment-{id}`, `#article-rating`, `#comments`), damit der Leser nicht an den Seitenanfang springt.

## 7. Offene Punkte (bekannt, noch nicht umgesetzt)

- **Rating-Toggle:** Ein erneuter Klick auf dieselbe Bewertung soll die Bewertung entfernen. Aktuell wird sie nur überschrieben.
- **Antworten auf Antworten sowie Bild-Uploads bei Antworten:** im UI nicht vollständig verfügbar.
- **Admin-Benutzerverwaltung:** Rollenwechsel synchronisiert Identity-Rollen und das `User.Role`-Feld. Dafür fehlen Tests.

## 8. Qualitätsregeln für Änderungen

- Bestehende Tests bleiben grün. Neue Regeln brauchen einen Test.
- Keine Schema-Änderung ohne vorherige Aktualisierung von `DataSchema.md`.
- Keine Zeitschätzungen in Plänen. Pläne als Todo-Listen mit einzelnen, prüfbaren Schritten formulieren.
- Pläne liegen unter `plans/`, Regeln wie diese Datei im Projektroot.
