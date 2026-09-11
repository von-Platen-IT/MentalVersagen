# BlogCMS — Entwicklungsanleitung

Dieses Dokument beschreibt, wie das CMS lokal gestartet wird, und fasst die
Sicherheitsentscheidungen zusammen. Das Pflichtenheft liegt unverändert im
Projekt-Root (`*.md`), der Code unter `src/`.

## Voraussetzungen

- .NET SDK 10
- Docker + Docker Compose

## Schnellstart

```bash
# 1. PostgreSQL starten (Container-Port 5432 → Host 5433)
docker compose up -d

# 2. Abhängigkeiten wiederherstellen und bauen
dotnet build src/BlogCms.slnx

# 3. Migrationen anwenden
dotnet ef database update \
  -p src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj \
  -s src/BlogCms.Web/BlogCms.Web.csproj

# 4. Anwendung starten
dotnet run --project src/BlogCms.Web/BlogCms.Web.csproj --urls http://localhost:5080
```

Der Host-Port ist bewusst **5433** (Konfliktvermeidung mit einer bereits laufenden
lokalen PostgreSQL auf 5432). Über die Umgebungsvariable `POSTGRES_PORT` änderbar.

### Konfiguration

- Connection-String: `appsettings.Development.json` bzw. überschreibbar über
  `ConnectionStrings__DefaultConnection` oder User Secrets — nie hartcodiert.
- Kommentare: `Comments`-Sektion (`ModerationMode`, `MaxCommentsPerMinute`, `EditWindowMinutes`).
- Medien: `Media`-Sektion (`Provider`, Limits, `ConvertToWebP`, S3-Zugangsdaten).
- Zahlungen: `Stripe`-Sektion (`SecretKey`, `WebhookSecret`, Plan-IDs).
- Newsletter: `Newsletter`-Sektion (`UnconfirmedRetentionDays`).

### Entwicklungs-Fakes (keine Credentials nötig)

| Bereich | Verhalten im Dev-Modus |
|---|---|
| E-Mail | `DevEmailSender` schreibt HTML-Dateien nach `bin/.../App_Data/emails` |
| Zahlungen | Ohne `Stripe:SecretKey` simuliert `FakeStripeService` Checkout/Portal; Webhook-Signaturprüfung bleibt real |
| Medien-Storage | `LocalDiskStorageService` speichert unter `wwwroot/uploads` (Provider `Local`) |

## Tests

```bash
dotnet test tests/BlogCms.Tests/BlogCms.Tests.csproj
```

Die Tests bilden zentrale Akzeptanzkriterien der Module 01–05 ab (Slug-Eindeutigkeit,
Markdown-Sanitizing, Kommentar-Moderation/Rate-Limit/Soft-Delete, Webhook-Signatur,
Abo-Status-Cache, anonyme Spenden, Newsletter-Double-Opt-in).

## Projektstruktur

```
src/BlogCms.Domain          Entitäten + Enums (Single Point of Truth: DataSchema.md)
src/BlogCms.Infrastructure  EF Core, Content, Comments, Media, Payments, Newsletter, Email
src/BlogCms.Web             Razor Pages (Präsentation) + MVC-Controller (RSS, Webhook)
tests/BlogCms.Tests         xUnit-Tests
```

## Rollen

Beim Start werden die Rollen `Reader`, `Premium`, `Moderator`, `Admin` idempotent
angelegt. Neue Konten erhalten `Reader`. Für den Zugriff auf die Verwaltung
(`/Admin/Articles`) bzw. Moderation (`/Moderation`) müssen Nutzer manuell per SQL
einer Rolle zugewiesen werden, z. B.:

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'admin@example.com' AND r."Name" = 'Admin';
```

## Sicherheit (Modul-übergreifend)

- **Markdown/XSS**: Rendering erfolgt serverseitig über Markdig und wird zwingend
  mit HtmlSanitizer bereinigt ([`MarkdownRenderer`](src/BlogCms.Infrastructure/Content/MarkdownRenderer.cs:1));
  `javascript:`-URLs werden entfernt.
- **Video-Embeds**: Es wird ausschließlich der offizielle oEmbed-Code verwendet,
  eingebettet in ein `<iframe sandbox="allow-scripts allow-popups">` ohne
  `allow-same-origin` ([`VideoEmbedRenderer`](src/BlogCms.Web/Content/VideoEmbedRenderer.cs:1)).
- **Webhooks**: Die Stripe-Signatur wird per HMAC-SHA256 inkl. Zeitstempel-Toleranz
  geprüft; ungültige Signaturen werden mit `401` abgewiesen
  ([`StripeSignatureVerifier`](src/BlogCms.Infrastructure/Payments/PaymentOptions.cs:1)).
- **Rollenprüfung**: Moderations- und Admin-Bereiche sind serverseitig über Policies
  geschützt, nicht nur UI-seitig ausgeblendet.
- **Paywall**: Der Premium-Zugriff basiert auf dem aktuellen Abo-Status
  (`User.SubscriptionStatus`), nicht auf der Rolle allein.
- **Uploads**: Bilder werden anhand des echten Datei-Inhalts validiert, skaliert,
  optional nach WebP konvertiert und ohne EXIF gespeichert.

## Bekannte Punkte / Ausblick

- EF meldet zwei Modell-Warnungen: Der Soft-Delete-Filter auf `Article` ist
  erforderliches Ende der Beziehungen zu `ArticleTag` und `Comment`. Aktuell
  unkritisch; künftig über passende Filter/optionale Navigationen zu bereinigen.
- Der echte Stripe-Provider (`StripeService`) nutzt die REST-API direkt; für
  Produktivbetrieb Zugangsdaten und ein öffentlich erreichbares Webhook-Secret setzen.
