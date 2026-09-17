# BlogCMS — Entwicklungsanleitung

Dieses Dokument beschreibt, wie das CMS lokal gestartet wird, und fasst die
Sicherheitsentscheidungen zusammen. Das Pflichtenheft liegt unverändert im
Projekt-Root (`*.md`), der Code unter `src/`.

## Voraussetzungen

- .NET SDK 10
- Docker + Docker Compose (für die lokale PostgreSQL; optional auch für die App)

## Client-Bibliotheken (LibMan / Bootstrap)

Bootstrap wird nicht eingecheckt, sondern über **LibMan** verwaltet
([`src/BlogCms.Web/libman.json`](src/BlogCms.Web/libman.json)); die Dateien landen
unter `src/BlogCms.Web/wwwroot/lib/bootstrap/dist/`.

Das NuGet-Paket `Microsoft.Web.LibraryManager.Build` (referenziert in
[`BlogCms.Web.csproj`](src/BlogCms.Web/BlogCms.Web.csproj)) führt den Restore
**automatisch** bei jedem `dotnet build`/`dotnet publish` aus. Es ist daher
**kein manueller Schritt und kein global installiertes Tool** nötig – weder auf
dem Host noch in Docker-Images oder CI (Voraussetzung: Netzwerkzugriff auf
`cdn.jsdelivr.net` beim Build). Beim `publish` werden die Bootstrap-Dateien in
die Ausgabe übernommen (inkl. vor-komprimierter `.br`/`.gz`-Varianten).

**Manueller Fallback** (z. B. IDE ohne MSBuild-Integration):

```bash
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
cd src/BlogCms.Web
libman restore
```

## Schnellstart

```bash
# 1. PostgreSQL starten (Container-Port 5432 → Host 5433)
docker compose up -d

# 2. Abhängigkeiten wiederherstellen und bauen
#    (holt Bootstrap automatisch via LibMan mit – siehe oben)
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
- Feature-Flags: `Features`-Sektion (`MonetizationEnabled`, `LoginEnabled`).

#### Feature-Flags (`.env`)

Zwei Schalter steuern die **UI-Sichtbarkeit** von Monetarisierung und Login:

| Parameter (`.env`) | Standard | Wirkung bei `false` |
|---|---|---|
| `Features__MonetizationEnabled` | `true` | Mitgliedschaft-/Spenden-/Premium-CTA werden nicht gerendert |
| `Features__LoginEnabled` | `true` | Anmelden-/Registrieren-Links und Anmelde-Hinweise werden nicht gerendert |

Die Werte sind reine UI-Steuerung: Alle Seiten, Routen und Policies bleiben aktiv.
`/Account/Login` und die Monetarisierungsseiten sind per Direkt-URL weiterhin
erreichbar; „Abmelden" bleibt für angemeldete Nutzer sichtbar.

**Laden der `.env`:** Beim Start liest die App eine `.env` im Arbeitsverzeichnis
oder in einem übergeordneten Ordner und setzt deren Einträge (`KEY=VALUE`) als
Umgebungsvariablen. Über die .NET-Konvention `__` werden sie auf die
`Features`-Sektion abgebildet. Reale Umgebungsvariablen (z. B. aus Docker) haben
Vorrang. Vorlage: [`.env.example`](.env.example) — einfach nach `.env` kopieren:

```bash
cp .env.example .env
```

### Entwicklungs-Fakes (keine Credentials nötig)

| Bereich | Verhalten im Dev-Modus |
|---|---|
| E-Mail | `DevEmailSender` schreibt HTML-Dateien nach `bin/.../App_Data/emails` |
| Zahlungen | Ohne `Stripe:SecretKey` simuliert `FakeStripeService` Checkout/Portal; Webhook-Signaturprüfung bleibt real |
| Medien-Storage | `LocalDiskStorageService` speichert unter `wwwroot/uploads` (Provider `Local`) |

## Docker

Es gibt zwei Varianten; beide nutzen dieselbe [`docker-compose.yml`](docker-compose.yml).

### Variante A — nur PostgreSQL im Container (Standard)

Wie im Schnellstart: `docker compose up -d` startet ausschließlich die
Datenbank, die App läuft auf dem Host per `dotnet run`. Der LibMan-/Bootstrap-
Restore passiert dabei automatisch beim `dotnet build` auf dem Host.

### Variante B — Datenbank **und** App im Container

Das [`Dockerfile`](Dockerfile) baut die Web-App als Multi-Stage-Image
(SDK-Build → schlankes ASP.NET-Runtime-Image, Ausführung als Nicht-Root-User).
Der Bootstrap-Restore läuft automatisch im Image-Build über `dotnet publish` –
es muss **nichts** zusätzlich installiert werden.

```bash
# 1. Image bauen und DB + App starten (Compose-Profil "app")
docker compose --profile app up -d --build

# 2. Migrationen anwenden (vom Host gegen die veröffentlichte DB auf Port 5433)
dotnet ef database update \
  -p src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj \
  -s src/BlogCms.Web/BlogCms.Web.csproj

# 3. Aufrufen: http://localhost:5080
```

Wichtige Punkte:

- Der App-Container erreicht die DB über den Compose-DNS-Namen `postgres`
  (nicht `localhost`). Der Connection-String wird per
  `ConnectionStrings__DefaultConnection` aus der Compose-Datei gesetzt und
  überschreibt damit `appsettings.Development.json`.
- Das Profil `app` lässt den Standard-Workflow unverändert: ein einfaches
  `docker compose up -d` startet weiterhin nur PostgreSQL.
- Hochgeladene Medien liegen im Volume `blogcms-uploads`
  (`/app/wwwroot/uploads`) und überleben einen Container-Neustart.
- Anpassbar über Umgebungsvariablen: `APP_PORT` (Host-Port, Standard `5080`),
  `ASPNETCORE_ENVIRONMENT` (Standard `Development`), `POSTGRES_USER`,
  `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT` sowie die Feature-Flags
  `Features__MonetizationEnabled` und `Features__LoginEnabled` — letztere werden
  aus der `.env` an den App-Container durchgereicht (Standard jeweils `true`).
- Die App wendet **keine** Migrationen automatisch an; Schritt 2 ist erforderlich.

Aufräumen:

```bash
docker compose --profile app down        # Container stoppen/entfernen
docker compose --profile app down -v     # zusätzlich Volumes löschen
```

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

Beim Start werden die Rollen `Reader`, `Author`, `Premium`, `Moderator`, `Admin`
idempotent angelegt. Neue Konten erhalten `Reader`. Für den Zugriff auf die
Verwaltung (`/Admin/Articles`, Rolle `Author` oder `Admin`), die Linklisten
(`/Admin/LinkLists`, `Admin`) bzw. Moderation (`/Moderation`, `Moderator`/`Admin`)
müssen Nutzer manuell per SQL einer Rolle zugewiesen werden, z. B.:

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'admin@example.com' AND r."Name" = 'Admin';
```

Es gibt **keinen vorkonfigurierten Admin-Benutzer** — die Rolle wird immer manuell
vergeben. Nach einer Rollenänderung muss man sich neu anmelden, damit die
Rollen-Claims im Auth-Cookie aktualisiert werden. Eine ausführliche Anleitung
(Admin/„Root" werden, Admin-Status in der DB prüfen, Rollen entziehen) sowie die
Nutzung von Admin-Bereich und Blog liegen unter [`docs/`](docs/README.md):

- [`docs/01-rollen-und-admin.md`](docs/01-rollen-und-admin.md)
- [`docs/02-admin-artikelverwaltung.md`](docs/02-admin-artikelverwaltung.md)
- [`docs/03-blog-nutzen.md`](docs/03-blog-nutzen.md)

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
