# Plan: Kategorien-API, Hashtag-API und URL-basierte Listenfilter

## 1. Ziel

Drei zusammenhängende Erweiterungen:

1. **Kategorien über die Management-API verwalten** — anlegen, ändern, löschen, lesen.
2. **Hashtags eines Artikels über die API ändern** — dediziert und ohne Voll-Update.
3. **Mehrfachfilter über die URL** — Artikellisten lassen sich per URL nach mehreren
   Kategorien und/oder mehreren Hashtags filtern. Die URL ist bookmark- und teilbar.
   Das Feature wird in der Bedienungsanleitung erklärt.

Keine Datenbankänderung nötig: Kategorien, Hashtags und die Verknüpfungen existieren
bereits. Es werden nur Anwendungslogik, API-Endpunkte, UI und Doku erweitert.

---

## 2. Ausgangslage (Ist)

| Bereich | Datei | Ist-Zustand |
|---|---|---|
| Management-API | [`ManagementArticlesController.cs`](src/BlogCms.Web/Controllers/ManagementArticlesController.cs) | Nur Artikel-CRUD; `ICategoryService` ist bereits injiziert, aber nur zum Auflösen des Kategorie-Slugs. |
| API-Modelle | [`ArticleApiModels.cs`](src/BlogCms.Web/Models/Api/ArticleApiModels.cs) | `ArticleWriteModel.Hashtags` existiert und wird beim Voll-Update mitgesendet. |
| Kategorie-Service | [`CategoryService.cs`](src/BlogCms.Infrastructure/Categories/CategoryService.cs) | Vollständiges CRUD inkl. Schutz von `undefined` und Artikel-Umzug beim Löschen. |
| Artikel-Service | [`ArticleService.cs`](src/BlogCms.Infrastructure/Content/ArticleService.cs) | `ArticleQuery` unterstützt genau **eine** `CategoryId` und **einen** `HashtagSlug`. |
| Öffentliche Liste | [`ArticlesController.cs`](src/BlogCms.Web/Controllers/ArticlesController.cs) | Query-Parameter `category`, `hashtag`, `query`, `sort`, `dateFrom`, `dateTo`. |
| Startseite | [`Index.cshtml`](src/BlogCms.Web/Pages/Index.cshtml) / [`site.js`](src/BlogCms.Web/wwwroot/js/site.js) | Ein Kategorie-Dropdown, ein Hashtag, clientseitiges Nachladen per REST. |
| Suche | [`Suche.cshtml`](src/BlogCms.Web/Pages/Suche.cshtml) / [`Suche.cshtml.cs`](src/BlogCms.Web/Pages/Suche.cshtml.cs) | Serverseitige GET-Filter, aber nur je eine Kategorie und ein Hashtag. |
| Doku | [`docs/05-management-api.md`](docs/05-management-api.md), [`docs/03-blog-nutzen.md`](docs/03-blog-nutzen.md) | Keine Kategorien-/Hashtag-Endpunkte, keine URL-Filter-Anleitung. |

---

## 3. API-Design

### 3.1 Kategorien: `/api/v1/categories`

Neuer Controller `ManagementCategoriesController` mit derselben Absicherung wie die
Artikel-API (`X-Api-Key`, Rate-Limit, `application/problem+json`).

| Methode | Route | Zweck | Antwort |
|---|---|---|---|
| `GET` | `/api/v1/categories` | Alle Kategorien inkl. `articleCount` | `200` Liste |
| `GET` | `/api/v1/categories/{idOrSlug}` | Einzelkategorie | `200` / `404` |
| `POST` | `/api/v1/categories` | Kategorie anlegen | `201` + `Location` |
| `PUT` | `/api/v1/categories/{idOrSlug}` | Kategorie ändern | `200` / `404` |
| `DELETE` | `/api/v1/categories/{idOrSlug}` | Kategorie löschen | `204` / `409` |

**Regeln (aus `CategoryService`):**
- `undefined` ist Systemkategorie: weder löschbar noch umbenennbar → `409 Conflict`.
- Beim Löschen werden zugeordnete Artikel auf `undefined` verschoben.
- `slug` wird bei Bedarf aus dem Namen generiert; Kollisionen werden aufgelöst.
- `name` ist Pflicht, maximal 100 Zeichen; `description`/`disclaimer` optional.

**Modelle** in `src/BlogCms.Web/Models/Api/CategoryApiModels.cs`:
- `CategoryWriteModel` — `name`, `slug?`, `description?`, `disclaimer?`, `displayOrder?`
- `CategoryReadModel` — `id`, `slug`, `name`, `description`, `disclaimer`,
  `displayOrder`, `isSystem`, `articleCount`

**Auflösung `{idOrSlug}`:** zuerst `Guid.TryParse` → `GetByIdAsync`, sonst
`GetBySlugAsync`. Kein Treffer → `404`.

### 3.2 Hashtags: `/api/v1/articles/{id}/hashtags` und `/api/v1/hashtags`

| Methode | Route | Zweck | Antwort |
|---|---|---|---|
| `GET` | `/api/v1/hashtags` | Alle Hashtags mit Nutzungszahl | `200` Liste |
| `PUT` | `/api/v1/articles/{id}/hashtags` | Hashtags eines Artikels **ersetzen** | `200` Artikel |
| `DELETE` | `/api/v1/articles/{id}/hashtags` | Alle Hashtags des Artikels entfernen | `204` |

- Body für `PUT`: `{ "hashtags": ["DeepDive", "Faktencheck"] }` (ohne führendes `#`).
- Leere Liste entfernt alle Hashtags.
- Unbekannte Hashtags werden angelegt (bestehende `SyncHashtagsAsync`-Logik).
- Das bestehende Voll-Update (`PUT /api/v1/articles/{id}`) akzeptiert `hashtags`
  weiterhin; die neuen Endpunkte sind der gezielte Weg ohne Voll-Update.

**Service-Erweiterung:** neue Methode in `IArticleService`:
```csharp
Task<Article?> SetHashtagsAsync(
    Guid articleId, IEnumerable<string> hashtagNames,
    CancellationToken cancellationToken = default);
```
Sie lädt den Artikel, ruft `SyncHashtagsAsync` auf, speichert und gibt den
aktualisierten Artikel (inkl. Hashtags) zurück. Kein Eingriff in Tags, Status oder
Medien.

### 3.3 Fehlerbilder

| Status | Fall |
|---|---|
| `400` | Validierungsfehler (`name` fehlt/zu lang, ungültiger Slug) |
| `401` | API-Key fehlt/ungültig |
| `404` | Kategorie/Artikel nicht gefunden |
| `409` | `undefined` löschen oder umbenennen |
| `429` | Rate-Limit |

---

## 4. URL-basierte Mehrfachfilter

### 4.1 Query-Parameter

| Parameter | Bedeutung | Beispiel |
|---|---|---|
| `categories` | Kommagetrennte Kategorie-Slugs (mehrfach erlaubt) | `categories=politik,satire` |
| `hashtags` | Kommagetrennte Hashtag-Slugs (mehrfach erlaubt) | `hashtags=deepdive,faktencheck` |
| `match` | `any` (Standard) oder `all` | `match=all` |
| `category` | Alias für genau eine Kategorie (rückwärtskompatibel) | `category=politik` |
| `hashtag` | Alias für genau einen Hashtag (rückwärtskompatibel) | `hashtag=deepdive` |
| `sort`, `query`, `dateFrom`, `dateTo`, `page` | unverändert | |

**Semantik:**
- `match=any` (Standard): Artikel passt, wenn er in **einer** der gewählten
  Kategorien liegt **oder** **einen** der gewählten Hashtags trägt.
- `match=all`: Artikel muss **alle** gewählten Hashtags tragen. Kategorien bleiben
  ODER-verknüpft, weil ein Artikel genau eine Kategorie hat.
- Ohne `categories`/`hashtags` bleibt das Verhalten unverändert.

**Beispiele für Bookmarks:**
```
/                                     → alle Akten
/?categories=politik,satire           → Politik oder Satire
/?hashtags=deepdive,faktencheck       → DeepDive oder Faktencheck
/?categories=politik&hashtags=deepdive&match=all
                                      → Politik und (DeepDive und ggf. weitere)
/Suche?categories=politik,satire&hashtags=deepdive&match=any&sort=MostViewed
```

### 4.2 Anwendungsschicht

`ArticleQuery` erweitern (neue optionale Parameter am Ende, damit bestehende
Aufrufe kompilieren):
```csharp
IReadOnlyList<Guid>? CategoryIds = null,
IReadOnlyList<string>? HashtagSlugs = null,
ArticleFilterMatch Match = ArticleFilterMatch.Any
```
Neues Enum `ArticleFilterMatch { Any, All }`.

`SearchPublishedAsync`:
- Kategorien: `articles.Where(a => categoryIds.Contains(a.CategoryId))` (ODER).
- Hashtags bei `Any`: `articles.Where(a => a.ArticleHashtags.Any(ah => hashtagSlugs.Contains(ah.Hashtag!.Slug.ToLower())))`.
- Hashtags bei `All`: für jeden Slug ein eigenes `Where(a => a.ArticleHashtags.Any(...))`
  anhängen (EF-übersetzbar, kein `All` auf Navigation).
- Bestehende Einzelparameter (`CategoryId`, `HashtagSlug`) bleiben als Fallback
  erhalten und werden mit den Listen zusammengeführt.

### 4.3 Öffentliche REST-Schnittstelle

`ArticlesController.Index` erhält zusätzlich:
```csharp
string? categories = null,
string? hashtags = null,
string? match = null
```
- Kommalisten parsen, trimmen, deduplizieren.
- Slugs über `ICategoryService.GetBySlugAsync` in IDs auflösen; unbekannte Slugs
  werden ignoriert (kein Fehler, damit Bookmarks robust bleiben).
- `match` per `Enum.TryParse<ArticleFilterMatch>` (case-insensitive).
- Antwort unverändert; optional `appliedFilters` im JSON für Debug/UI.

### 4.4 Startseite

- `IndexModel.OnGetAsync(string? categories, string? hashtags, string? match, string? sort)`
  liest die Filter und ruft `SearchPublishedAsync` mit den Mehrfachlisten auf.
- Die Startseite rendert die Treffer serverseitig (funktioniert ohne JavaScript).
- `Index.cshtml`:
  - Das Kategorie-Dropdown bleibt für die schnelle Einfachauswahl.
  - Ein neuer Hinweis/Button **„Diese Auswahl als Lesezeichen speichern“** kopiert
    die aktuelle URL in die Zwischenablage (`navigator.clipboard`) und zeigt eine
    kurze Bestätigung. Fallback: URL wird in einem Textfeld markiert.
  - Wenn die URL mehrere Kategorien/Hashtags enthält, zeigt eine Statuszeile die
    aktiven Filter als Chips mit „×“ zum Entfernen (entfernt den Parameter und
    lädt neu).
- `site.js`:
  - Beim Initialisieren `categories`, `hashtags`, `match` aus `location.search`
    lesen und an `/api/articles` senden.
  - Bei Filter-/Sortieränderung `history.replaceState` mit der neuen URL, damit
    die Adresszeile sofort bookmark-fähig ist.
  - `loadArticles` sendet `categories`/`hashtags`/`match` statt nur `category`/`hashtag`,
    wenn Mehrfachfilter aktiv sind.

### 4.5 Suchseite

- `SucheModel.OnGetAsync` erhält `categories`, `hashtags`, `match` zusätzlich zu
  den bestehenden Parametern.
- `Suche.cshtml`:
  - Kategorien und Hashtags als `<select multiple>` (Mehrfachauswahl).
  - Neues Select **Verknüpfung**: „Mindestens ein Filter (ODER)“ / „Alle Hashtags (UND)“.
  - Pagination-Links geben die neuen Parameter mit weiter.
- Die Seite bleibt vollständig serverseitig und ohne JavaScript nutzbar.

---

## 5. Bedienungsanleitung

- [`docs/03-blog-nutzen.md`](docs/03-blog-nutzen.md) erhält einen neuen Abschnitt
  **„Listen filtern und als Lesezeichen speichern“**:
  - Erklärung der Parameter `categories`, `hashtags`, `match`.
  - Drei bis vier konkrete Beispiel-URLs.
  - Anleitung: Filter setzen → „Als Lesezeichen speichern“ → Browser-Lesezeichen.
  - Hinweis, dass die URL teilbar ist und ohne Login funktioniert.
- [`docs/05-management-api.md`](docs/05-management-api.md) erhält:
  - Abschnitt **Kategorien verwalten** mit curl-Beispielen für alle fünf Endpunkte.
  - Abschnitt **Hashtags eines Artikels ändern** mit `PUT`/`DELETE`-Beispielen.
  - Aktualisierte Endpunkt-Tabelle und `operationId`s.
- [`docs/README.md`](docs/README.md) verlinkt den neuen Abschnitt.
- [`README.md`](README.md) erhält einen kurzen Satz zu den URL-Filtern.

---

## 6. Tests

| Testdatei | Inhalt |
|---|---|
| `CategoryApiTests.cs` (neu) | CRUD über Controller, `undefined`-Schutz (409), 404, Artikel-Umzug beim Löschen, `articleCount` |
| `ArticleHashtagApiTests.cs` (neu) | `SetHashtagsAsync` ersetzt/leert, legt unbekannte Hashtags an, Controller `PUT`/`DELETE` |
| `ArticleFilterTests.cs` (neu) | Mehrfachkategorien (ODER), Mehrfachhashtags `any`/`all`, Kombination, unbekannte Slugs werden ignoriert |
| `ApiDocumentationTests.cs` (erweitert) | Neue Operationen erscheinen in der OpenAPI-Spec |
| `ContentTests.cs` (erweitert) | `ArticleQuery`-Defaults bleiben kompatibel |

Ziel: bestehende 271 Tests bleiben grün, neue Tests decken die Erweiterungen ab.

---

## 7. Umsetzungsreihenfolge

1. **Filter-Kern**: `ArticleFilterMatch`, `ArticleQuery` erweitern, `SearchPublishedAsync`
   anpassen, Tests für Mehrfachfilter.
2. **Hashtag-Service**: `SetHashtagsAsync` in `IArticleService`/`ArticleService`,
   Tests.
3. **Kategorien-API**: Modelle, `ManagementCategoriesController`, Tests.
4. **Hashtag-API**: Endpunkte im `ManagementArticlesController` + `GET /api/v1/hashtags`,
   Tests.
5. **Öffentliche REST-Schnittstelle**: `ArticlesController` um Mehrfachfilter erweitern.
6. **Startseite**: `IndexModel`, `Index.cshtml`, `site.js` (URL-Sync, Bookmark-Button,
   Filter-Chips).
7. **Suchseite**: `SucheModel`, `Suche.cshtml` (Mehrfachauswahl, `match`).
8. **Doku**: `docs/03`, `docs/05`, `docs/README.md`, `README.md`.
9. **Abschluss**: Build, komplette Testsuite, manueller Smoke-Test der Beispiel-URLs.

---

## 8. Risiken und Entscheidungen

- **Keine Migration** — das Datenmodell bleibt unverändert.
- **Rückwärtskompatibilität** — `category`/`hashtag` bleiben als Aliase erhalten;
  bestehende Bookmarks funktionieren weiter.
- **`match=all` bei Kategorien** ist fachlich sinnlos (ein Artikel hat genau eine
  Kategorie). Kategorien werden daher immer ODER-verknüpft; `match` wirkt nur auf
  Hashtags. Das wird in der Doku erklärt.
- **Unbekannte Slugs** in der URL werden ignoriert statt einen Fehler zu erzeugen,
  damit alte Bookmarks nach dem Löschen einer Kategorie nicht brechen.
- **Kein Login nötig** — die Filter-URLs sind öffentlich und teilbar.
