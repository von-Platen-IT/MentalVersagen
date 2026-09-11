# 00 — Übersicht

## Projektziel

Ein selbst programmiertes CMS (ASP.NET) für einen Blog mit den Themenschwerpunkten **Politik, Satire und Verschwörungstheorien**. Ziel ist der Aufbau einer treuen Leserschaft in der Prelaunch-Phase mit späterer Monetarisierung über Membership/Spenden.

## Tech-Stack (angenommen, anpassbar)

- Backend: ASP.NET Core (MVC oder Razor Pages), Entity Framework Core
- Auth: ASP.NET Core Identity
- Datenbank: relational (z. B. PostgreSQL oder SQL Server) — Referenz: `DataSchema.md`
- Objekt-Storage für Medien: S3-kompatibel (z. B. Cloudflare R2) oder Azure Blob Storage
- Zahlungen: Stripe (Abos), optional PayPal (Einzelspenden)
- Newsletter: Double-Opt-in, initial per SMTP/Transaktionsmail-Anbieter (z. B. Brevo/Mailjet)

## Infrastruktur (lokale Entwicklung)

- **Datenbank:** PostgreSQL, betrieben in einem Docker-Container (lokale Entwicklungsumgebung). EF Core nutzt hierfür den `Npgsql.EntityFrameworkCore.PostgreSQL`-Provider. `DataSchema.md` ist bewusst DB-agnostisch formuliert und erfordert dafür keine Anpassung.
- Primärschlüssel als `Guid` (Postgres-Spaltentyp `uuid`) — unproblematisch mit Postgres.
- Bereitstellung über ein `docker-compose.yml` im Projekt-Root (Postgres-Service, benannter Volume-Mount für Persistenz, Standardport `5432` oder projektspezifisch abweichend gemappt).
- Connection-String wird über Umgebungsvariablen bzw. `appsettings.Development.json` konfiguriert, nicht hartcodiert — ermöglicht identisches Vorgehen für Coding Agents in der lokalen Entwicklungsumgebung.
- EF Core Migrations laufen wie gewohnt gegen die Docker-Postgres-Instanz (`dotnet ef database update`), kein gesonderter Workflow nötig.
- Produktion: Wahl des Postgres-Hostings (z. B. verwalteter Dienst vs. eigener Container) ist zum jetzigen Zeitpunkt noch offen und wird an dieser Stelle ergänzt, sobald entschieden.

## Nicht-funktionale Anforderungen

- **Moderationsfähigkeit**: Aufgrund des kontroversen Themenfelds müssen Kommentare und hochgeladene Medien vor Veröffentlichung geprüft oder zumindest meldbar sein (siehe `02-Kommentarfunktion.md`).
- **Kennzeichnungspflicht**: Artikel-Kategorie (`Politik` / `Satire` / `Verschwoerungstheorien`) ist Pflichtfeld — dient als Grundlage für spätere Disclaimer-Anzeige und redaktionelle Trennung.
- **Skalierbarkeit von Medien**: Bilder werden nicht im Anwendungsserver, sondern in Objekt-Storage abgelegt.
- **Unabhängigkeit von Drittplattformen**: Kernfunktionen (Artikel, Kommentare, Newsletter-Liste) laufen auf eigener Infrastruktur, nicht auf einer Plattform mit eigenen Content-Richtlinien.

## Out of Scope (v1)

- Eigenes Video-Hosting (nur Verknüpfung externer Plattformen, siehe `03-Medien-Upload-und-Embedding.md`)
- Mehrsprachigkeit
- Native Mobile Apps
