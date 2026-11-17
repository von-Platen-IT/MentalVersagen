# 00 — Übersicht

## Projektziel

Ein selbst programmiertes CMS (ASP.NET) für einen Blog mit **dynamisch vom
Administrator verwalteten Themenkategorien** (z. B. Politik, Satire,
Verschwörungstheorien). Ziel ist der Aufbau einer treuen Leserschaft in der
Prelaunch-Phase mit späterer Monetarisierung über Membership/Spenden.

## Fachliche Referenz (Block FeatureFix1)

Die übergreifende, technologieunabhängige Regelsammlung für das Blog liegt in
[`FeatureFix1.MD`](FeatureFix1.MD). Sie ist die fachliche Referenz für Rollen
(Viewer/Autor/Administrator), Beiträge (inkl. Titelbild, Kurzbeschreibung, Tags/Hashtags,
geplanter Veröffentlichung), Zugriffsstufen, Bewertungen, Kommentare, Linklisten und RSS.

Der daraus abgeleitete Soll-Ist-Abgleich und der Umsetzungsplan liegen in
[`feature_implementation1.md`](feature_implementation1.md); das fortgeschriebene Datenmodell
in [`DataSchema.md`](DataSchema.md).

**Rollen-Mapping:** Viewer ≙ `Reader`, Autor ≙ `Author`, Administrator ≙ `Admin`
(`Moderator` und `Premium` bleiben als zusätzliche Rollen erhalten).

## Tech-Stack (umgesetzt)

- Backend: ASP.NET Core 10, Razor Pages + MVC-Controller, Entity Framework Core 10
- Auth: ASP.NET Core Identity
- Datenbank: PostgreSQL (Npgsql); Tests mit EF-InMemory
- Medien-Storage: `LocalDiskStorageService` (Default) oder S3-kompatibler
  `S3StorageService` (`Media:Provider`)
- Zahlungen: Stripe.net (Mitgliedschaft über Checkout, Webhook und Sync);
  Spenden-Backend vorhanden, UI noch Platzhalter
- Newsletter: Double-Opt-in über SMTP (`SmtpEmailSender`) bzw. Dev-Datei
- Markdown: Markdig + HtmlSanitizer; Bilder: Magick.NET

## Infrastruktur (lokale Entwicklung)

- **Datenbank:** PostgreSQL, betrieben in einem Docker-Container (lokale Entwicklungsumgebung). EF Core nutzt hierfür den `Npgsql.EntityFrameworkCore.PostgreSQL`-Provider. `DataSchema.md` ist bewusst DB-agnostisch formuliert und erfordert dafür keine Anpassung.
- Primärschlüssel als `Guid` (Postgres-Spaltentyp `uuid`) — unproblematisch mit Postgres.
- Bereitstellung über ein `docker-compose.yml` im Projekt-Root (Postgres-Service, benannter Volume-Mount für Persistenz, Standardport `5432` oder projektspezifisch abweichend gemappt).
- Connection-String wird über Umgebungsvariablen bzw. `appsettings.Development.json` konfiguriert, nicht hartcodiert — ermöglicht identisches Vorgehen für Coding Agents in der lokalen Entwicklungsumgebung.
- EF Core Migrations laufen wie gewohnt gegen die Docker-Postgres-Instanz (`dotnet ef database update`), kein gesonderter Workflow nötig.
- Produktion: Wahl des Postgres-Hostings (z. B. verwalteter Dienst vs. eigener Container) ist zum jetzigen Zeitpunkt noch offen und wird an dieser Stelle ergänzt, sobald entschieden.

## Nicht-funktionale Anforderungen

- **Moderationsfähigkeit**: Aufgrund des kontroversen Themenfelds müssen Kommentare und hochgeladene Medien vor Veröffentlichung geprüft oder zumindest meldbar sein (siehe `02-Kommentarfunktion.md`).
- **Kennzeichnungspflicht**: Jeder Artikel verweist auf genau eine dynamische
  Kategorie; ohne Auswahl greift die geschützte Systemkategorie `undefined`.
  Ein optionaler Disclaimer pro Kategorie dient der redaktionellen Trennung.
  Zusätzlich kennzeichnet `Article.Origin` den Ursprung (Mensch/KI/Hybrid).
- **Skalierbarkeit von Medien**: Bilder werden standardmäßig lokal unter
  `wwwroot/uploads` gespeichert; für Produktion ist S3-kompatibler Storage
  konfigurierbar.
- **Unabhängigkeit von Drittplattformen**: Kernfunktionen (Artikel, Kommentare, Newsletter-Liste) laufen auf eigener Infrastruktur, nicht auf einer Plattform mit eigenen Content-Richtlinien.

## Out of Scope (v1)

- Eigenes Video-Hosting (nur Verknüpfung externer Plattformen, siehe `03-Medien-Upload-und-Embedding.md`)
- Mehrsprachigkeit
- Native Mobile Apps
