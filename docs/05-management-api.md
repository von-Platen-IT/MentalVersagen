# Management-API: Artikel per API pflegen

Die Management-API erlaubt es, Blog-Artikel **anzulegen**, **zu bearbeiten** und **zu löschen** — ohne die Web-Oberfläche, per `curl`, Skript oder **KI-Tool**. Sie ist abgesichert über einen statischen API-Key und vollständig über Swagger/OpenAPI dokumentiert.

> Technischer Plan und Architektur: [`plans/api-artikelverwaltung.md`](../plans/api-artikelverwaltung.md)

---

## 1. Einrichtung

### 1.1 API-Key erzeugen

```bash
openssl rand -hex 32
```

### 1.2 Konfiguration in `.env` setzen

```bash
# Statischer API-Key — NIE committen, nur in .env bzw. als Umgebungsvariable
Api__Key=<hier den erzeugten Key eintragen>

# UUID eines bestehenden Benutzers mit Rolle Author oder Admin.
# Über die API angelegte Artikel werden diesem Autor zugeordnet.
Api__AuthorId=<UUID des Autors>

# Optional: interaktive Swagger-Oberfläche unter /swagger (Standard: aus)
Api__SwaggerUiEnabled=true

# Optional: Anfragen-Limit pro Minute und Client-IP (Standard: 60)
Api__RateLimitPerMinute=60
```

Die Benutzer-ID findet sich in der Admin-Verwaltung (`/Admin/Users`) oder direkt in der Tabelle `AspNetUsers`.

### 1.3 Verhalten beim Start

- Ohne `Api__Key` bleibt die API **geschlossen** (Fail-Closed): Jeder Zugriff auf `/api/v1` antwortet mit 401. Die Anwendung protolliert eine Warnung.
- Ohne `Api__AuthorId` ist nur das **Anlegen** neuer Artikel nicht möglich (Antwort 503); Lesen, Bearbeiten und Löschen funktionieren weiterhin.
- Ein Key kürzer als 32 Zeichen erzeugt eine Warnung im Log — bitte einen längeren Zufallskey verwenden.

---

## 2. Endpunkte

Basis-URL: `https://<domain>/api/v1` — Authentifizierung über den Header **`X-Api-Key`**.

| Methode | Route | Zweck |
|---|---|---|
| `GET` | `/api/v1/articles` | Artikel auflisten (Filter: `status`, `query`, `page`, `pageSize`) |
| `GET` | `/api/v1/articles/{id}` | Einzelnen Artikel lesen (inkl. Markdown-Quelltext) |
| `POST` | `/api/v1/articles` | Artikel **anlegen** |
| `PUT` | `/api/v1/articles/{id}` | Artikel **bearbeiten** (Voll-Update, inkl. Statuswechsel) |
| `DELETE` | `/api/v1/articles/{id}` | Artikel **löschen** (Soft-Delete) |

### Statuswerte (`status`)

| Wert | Bedeutung |
|---|---|
| `Draft` | Entwurf — darf unvollständig sein |
| `Published` | Sofort veröffentlichen — erfordert `excerpt`, `contentMarkdown` und eine Titelbild-Quelle |
| `Scheduled` | Zeitgesteuert veröffentlichen — zusätzlich `scheduledAt` in der Zukunft (ISO-8601, UTC) |
| `Archived` | Archiviert |

Weitere Enums: `category` (`Politik`, `Satire`, `Verschwoerungstheorien`), `accessLevel` (`Public`, `Registered`, `Premium`).

---

## 3. Beispiele (curl)

### Artikel anlegen

```bash
curl -X POST https://<domain>/api/v1/articles \
  -H "X-Api-Key: <KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Mein erster API-Beitrag",
    "excerpt": "Ein kurzer Teaser.",
    "contentMarkdown": "# Überschrift\n\nDer Artikeltext in **Markdown**.",
    "category": "Politik",
    "accessLevel": "Public",
    "status": "Published",
    "titleImageUrl": "https://example.com/titelbild.jpg",
    "tags": ["Innenpolitik"],
    "hashtags": ["DeepDive"]
  }'
```

Antwort: `201 Created` mit `Location`-Header und dem gespeicherten Artikel (inkl. vergebener `id` und `slug`).

### Artikel bearbeiten (z. B. veröffentlichen)

```bash
curl -X PUT https://<domain>/api/v1/articles/<ID> \
  -H "X-Api-Key: <KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Mein erster API-Beitrag",
    "excerpt": "Ein kurzer Teaser.",
    "contentMarkdown": "# Überschrift\n\nAktualisierter Text.",
    "category": "Politik",
    "accessLevel": "Public",
    "status": "Published",
    "titleImageUrl": "https://example.com/titelbild.jpg"
  }'
```

`PUT` ist ein Voll-Update: Alle Felder werden aus dem Request übernommen. Statuswechsel (Veröffentlichen, Planen, Zurückziehen, Archivieren) erfolgen ebenfalls über `status`.

### Artikel auflisten und lesen

```bash
# Alle Entwürfe finden
curl -H "X-Api-Key: <KEY>" "https://<domain>/api/v1/articles?status=Draft&page=1&pageSize=20"

# Einzelnen Artikel inkl. Markdown-Quelltext lesen
curl -H "X-Api-Key: <KEY>" https://<domain>/api/v1/articles/<ID>
```

### Artikel löschen

```bash
curl -X DELETE https://<domain>/api/v1/articles/<ID> -H "X-Api-Key: <KEY>"
```

Antwort: `204 No Content`. Es handelt sich um einen **Soft-Delete** — der Artikel bleibt in der Datenbank erhalten, ist aber öffentlich und in der Verwaltung nicht mehr sichtbar.

---

## 4. Fehlerbehandlung

Alle Fehler antworten als `application/problem+json` (RFC 7807):

```json
{
  "type": "https://mentalversagen.de/problems/validation-error",
  "title": "Die Eingaben sind ungültig.",
  "status": 400,
  "errors": [
    { "field": "ImageUpload", "message": "Bitte eine Titelbild-URL angeben oder ein Bild hochladen (FeatureFix1 BR-022)." }
  ]
}
```

| Status | Typ | Bedeutung |
|---|---|---|
| 400 | `validation-error` | Eingaben prüfen — die betroffenen Felder stehen in `errors[]` |
| 401 | `unauthorized` | Fehlender oder ungültiger API-Key |
| 404 | `not-found` | Unbekannte Artikel-ID |
| 429 | `rate-limit` | Anfragen-Limit überschritten (Standard 60/min pro IP) |
| 503 | `not-configured` | `Api__AuthorId` fehlt (nur beim Anlegen) |
| 500 | `internal-error` | Unerwarteter Serverfehler |

---

## 5. Nutzung mit KI-Tools

Die API ist darauf ausgelegt, von LLM-Clients (z. B. **ChatGPT Actions**, **Claude-Connectoren**, eigene Agenten) gesteuert zu werden.

1. **Spec-URL** im Tool hinterlegen: `https://<domain>/swagger/v1/swagger.json`
2. **Authentifizierung** wählen: „API Key", Header-Name `X-Api-Key`, Wert = der konfigurierte Key.
3. Die Spec enthält eine Kurzanleitung, die fünf `operationId`s (`listArticles`, `getArticle`, `createArticle`, `updateArticle`, `deleteArticle`) und Feldbeschreibungen inkl. Formatangaben.

Empfohlener Ablauf für automatisierte Clients:

1. `listArticles` (ggf. mit `status`- oder `query`-Filter) — IDs und Slugs finden
2. `getArticle` — bestehenden Inhalt lesen
3. `createArticle` — Antwort `201` enthält die neue `id`
4. `updateArticle` — Änderungen vornehmen (Voll-Update!)
5. `deleteArticle` — Soft-Delete

**Hinweise:**
- Validierungsfehler (400) nennen die korrigierbaren Felder direkt in `errors[]` — einfach die genannten Felder anpassen und erneut senden.
- Enums werden als Klartext übertragen (`"Published"`, nicht `2`).
- Die interaktive Swagger-Oberfläche (`/swagger`) ist in der Entwicklung standardmäßig aktiv und in Produktion über `Api__SwaggerUiEnabled=true` zuschaltbar. Die maschinenlesbare Spec bleibt davon unberührt **immer** erreichbar.

---

## 6. Sicherheit

- Übertragung des Keys ausschließlich über HTTPS im Header `X-Api-Key`.
- Der Vergleich erfolgt **konstantzeitlich** (SHA-256 + `CryptographicOperations.FixedTimeEquals`) — keine Timing-Lecks.
- Der Key wird zu keinem Zeitpunkt geloggt.
- Ohne konfigurierten Key bleibt die API geschlossen (Fail-Closed).
- Rate-Limiting pro Client-IP begrenzt Brute-Force- und Missbrauchsversuche.
