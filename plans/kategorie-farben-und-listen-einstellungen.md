# Plan: Kategorie-Farben und Admin-Einstellungen für Listen

## 1. Ziel

Drei UI-/Admin-Verbesserungen:

1. **Kategorie-Farben in Listen:** Jeder Beitragseintrag erhält einen **3 px dicken
   Rahmen** in der Farbe seiner Kategorie. Die Kategorie ist damit auf einen Blick
   erkennbar.
2. **Farbe pro Kategorie administrierbar:** Im Admin-Bereich
   (`/Admin/Categories`) lässt sich für jede Kategorie eine Farbe wählen.
3. **Beiträge pro Liste administrierbar:** Der Administrator legt fest, wie viele
   Beiträge gleichzeitig in einer Liste (Startseite, Suche, REST-Grid) angezeigt
   werden.

Keine Änderung an der öffentlichen API-Semantik; die Kategorien-API wird um das
Feld `color` erweitert.

---

## 2. Ausgangslage

| Bereich | Datei | Ist-Zustand |
|---|---|---|
| Kategorie-Entität | [`Category.cs`](src/BlogCms.Domain/Entities/Category.cs) | Kein Farbfeld. |
| Kategorie-Konfiguration | [`CategoryConfiguration.cs`](src/BlogCms.Infrastructure/Data/Configurations/CategoryConfiguration.cs) | Keine Farbe. |
| Kategorie-Service | [`CategoryService.cs`](src/BlogCms.Infrastructure/Categories/CategoryService.cs) | `CategoryInput` ohne Farbe. |
| Admin-Kategorien | [`Admin/Categories/Index.cshtml`](src/BlogCms.Web/Pages/Admin/Categories/Index.cshtml), [`Edit.cshtml`](src/BlogCms.Web/Pages/Admin/Categories/Edit.cshtml) | Kein Farbwähler. |
| Karten-Partial | [`_ArticleCard.cshtml`](src/BlogCms.Web/Pages/Shared/_ArticleCard.cshtml) | Karte ohne Kategorie-Farbe. |
| Karten-CSS | [`site.css`](src/BlogCms.Web/wwwroot/css/site.css:936) | `.mv-case` mit 1 px Rahmen. |
| JS-Karten | [`site.js`](src/BlogCms.Web/wwwroot/js/site.js:256) | `renderCard` ohne Farbe. |
| Startseite | [`IndexModel`](src/BlogCms.Web/Pages/Index.cshtml.cs:12) | `LatestCount = 6` fest. |
| Suche | [`SucheModel`](src/BlogCms.Web/Pages/Suche.cshtml.cs:18) | `PageSize = 10` fest. |
| REST-Grid | [`ArticlesController`](src/BlogCms.Web/Controllers/ArticlesController.cs:40) | `pageSize = 6` fest. |
| Einstellungen | — | Keine Admin-Seite für Darstellungsoptionen. |

---

## 3. Kategorie-Farbe

### 3.1 Datenmodell

`Category` erhält:
```csharp
/// <summary>Hex-Farbe (#RRGGBB) für den Rahmen in Listen.</summary>
public string Color { get; set; } = "#7f8c8d";
```

`CategoryConfiguration`:
```csharp
builder.Property(c => c.Color)
    .IsRequired()
    .HasMaxLength(7)
    .HasDefaultValue("#7f8c8d");
```

**Migration** `AddCategoryColorAndSiteSettings`:
- `AddColumn<string>("Color", "Categories", maxLength: 7, defaultValue: "#7f8c8d")`.
- Anschließend `UPDATE`-Backfill für die bestehenden Kategorien:
  - `undefined` → `#7f8c8d`
  - `politik` → `#b03a2e`
  - `satire` → `#b7950b`
  - `verschwoerungstheorien` → `#6c3483`
- `Down`: Spalte entfernen.

### 3.2 Service

`CategoryInput` erhält am Ende `string? Color = null` (bestehende Aufrufe bleiben
kompatibel). `CategoryService`:
- Neue Hilfsmethode `NormalizeColor(string? color, string fallback)`:
  - akzeptiert `#RRGGBB` (case-insensitiv), sonst Fallback.
- `CreateAsync`: `Color = NormalizeColor(input.Color, "#7f8c8d")`.
- `UpdateAsync`: Farbe aktualisieren; bei Systemkategorie ebenfalls erlaubt
  (Farbe ist keine Umbenennung).

### 3.3 Admin-UI

- [`Admin/Categories/Index.cshtml`](src/BlogCms.Web/Pages/Admin/Categories/Index.cshtml):
  - Neue Spalte **Farbe** mit Farbquadrat (`<span class="mv-color-swatch" style="--swatch: @color">`).
  - Im Formular „Neue Kategorie“ ein `<input type="color" asp-for="Color">` plus
    Textfeld für den Hex-Wert.
- [`Admin/Categories/Edit.cshtml`](src/BlogCms.Web/Pages/Admin/Categories/Edit.cshtml):
  - Feld **Farbe** mit Farbwähler und Hex-Eingabe.
- [`Index.cshtml.cs`](src/BlogCms.Web/Pages/Admin/Categories/Index.cshtml.cs) und
  [`Edit.cshtml.cs`](src/BlogCms.Web/Pages/Admin/Categories/Edit.cshtml.cs):
  - `[BindProperty] public string Color { get; set; } = "#7f8c8d";`
  - Beim Laden/Anlegen/Speichern durchreichen.

### 3.4 Darstellung in Listen

- [`CategoryDisplay.cs`](src/BlogCms.Web/Content/CategoryDisplay.cs):
  ```csharp
  public const string DefaultColor = "#7f8c8d";
  public static string Color(Category? category) =>
      string.IsNullOrWhiteSpace(category?.Color) ? DefaultColor : category!.Color;
  ```
- [`_ArticleCard.cshtml`](src/BlogCms.Web/Pages/Shared/_ArticleCard.cshtml):
  ```html
  <article class="mv-case" style="--category-color: @CategoryDisplay.Color(article.Category);">
  ```
- [`site.css`](src/BlogCms.Web/wwwroot/css/site.css:936):
  ```css
  .mv-case {
    border: 3px solid var(--category-color, var(--mv-border));
  }
  ```
  Der Hover-Effekt überschreibt die Rahmenfarbe nicht mehr; der Glow bleibt über
  `box-shadow` erhalten.
- [`site.js`](src/BlogCms.Web/wwwroot/js/site.js:256) `renderCard`:
  ```js
  card.style.setProperty('--category-color', article.categoryColor || '');
  ```
- [`ArticlesController`](src/BlogCms.Web/Controllers/ArticlesController.cs:71):
  JSON um `categoryColor = CategoryDisplay.Color(a.Category)` erweitern.

### 3.5 Kategorien-API

- `CategoryWriteModel` und `CategoryReadModel` erhalten `Color`.
- `CategoryReadModel.FromEntity` übernimmt `category.Color`.
- `ManagementCategoriesController.ToInput` reicht `model.Color` weiter.
- [`docs/05-management-api.md`](docs/05-management-api.md) um `color` ergänzen.

---

## 4. Beiträge pro Liste (Admin-Einstellung)

### 4.1 Datenmodell

Neue Entität `SiteSetting`:
```csharp
public class SiteSetting
{
    public string Key { get; set; } = string.Empty;   // PK, max 100
    public string Value { get; set; } = string.Empty; // max 500
    public DateTime UpdatedAt { get; set; }
}
```

`AppDbContext` erhält `DbSet<SiteSetting> Settings`. Neue
`SiteSettingConfiguration` (Key als PK, max 100; Value max 500).

### 4.2 Service

Neue Datei `src/BlogCms.Infrastructure/Settings/SettingsService.cs`:
```csharp
public static class SettingKeys
{
    public const string ArticlesPerPage = "Display:ArticlesPerPage";
    public const int DefaultArticlesPerPage = 6;
    public const int MinArticlesPerPage = 1;
    public const int MaxArticlesPerPage = 50;
}

public interface ISettingsService
{
    Task<int> GetArticlesPerPageAsync(CancellationToken cancellationToken = default);
    Task SetArticlesPerPageAsync(int value, CancellationToken cancellationToken = default);
}
```
- `GetArticlesPerPageAsync`: liest den Key, parst int, clampt auf 1–50, Fallback 6.
- `SetArticlesPerPageAsync`: clampen, Upsert, `SaveChanges`.
- Registrierung in [`Program.cs`](src/BlogCms.Web/Program.cs) als Scoped.

### 4.3 Admin-Seite

Neue Razor-Page `src/BlogCms.Web/Pages/Admin/Settings/Index.cshtml(.cs)`:
- `[Authorize(Policy = Policies.RequireAdmin)]`
- Formular mit **„Beiträge pro Liste“** (`<input type="number" min="1" max="50">`).
- Speichern über `OnPostAsync`, TempData-Bestätigung.
- Link in der Admin-Navigation ([`_Layout.cshtml`](src/BlogCms.Web/Pages/Shared/_Layout.cshtml))
  unter „Einstellungen“.

### 4.4 Verwendung

- [`IndexModel`](src/BlogCms.Web/Pages/Index.cshtml.cs): `ISettingsService`
  injizieren, `LatestCount` durch `await _settings.GetArticlesPerPageAsync()` ersetzen.
- [`SucheModel`](src/BlogCms.Web/Pages/Suche.cshtml.cs): `PageSize` durch den
  Einstellungswert ersetzen.
- [`ArticlesController`](src/BlogCms.Web/Controllers/ArticlesController.cs):
  `ISettingsService` injizieren; `pageSize`-Default aus der Einstellung, weiterhin
  auf `MaxPageSize` begrenzt.
- [`Index.cshtml`](src/BlogCms.Web/Pages/Index.cshtml:45): `data-page-size` und
  `data-filtered-page-size` aus dem Model setzen (z. B. `Model.ArticlesPerPage`).

---

## 5. Tests

| Testdatei | Inhalt |
|---|---|
| `CategoryServiceTests.cs` (erweitert) | Farbe wird gespeichert, ungültige Farbe fällt auf Default zurück, Systemkategorie darf Farbe ändern |
| `CategoryApiTests.cs` (erweitert) | `color` in Create/Read/Update |
| `SettingsServiceTests.cs` (neu) | Default 6, Setzen/Lesen, Clamping 1–50 |
| `ArticleFilterTests.cs` | unverändert grün |
| `ApiDocumentationTests.cs` | unverändert grün |

Ziel: bestehende 292 Tests bleiben grün, neue Tests decken Farbe und Einstellung ab.

---

## 6. Dokumentation

- [`DataSchema.md`](DataSchema.md): `Category.Color` und neue Entität `SiteSetting`.
- [`docs/02-admin-artikelverwaltung.md`](docs/02-admin-artikelverwaltung.md):
  Kategorie-Farbe und Einstellungen für Beiträge pro Liste.
- [`docs/05-management-api.md`](docs/05-management-api.md): `color` in der
  Kategorien-API.
- [`docs/03-blog-nutzen.md`](docs/03-blog-nutzen.md): Hinweis, dass der farbige
  Rahmen die Kategorie zeigt.
- [`README.md`](README.md): kurzer Absatz zu Kategorie-Farben und Listenlänge.

---

## 7. Umsetzungsreihenfolge

1. **Datenmodell + Migration**: `Category.Color`, `SiteSetting`, Konfigurationen,
   Migration mit Farb-Backfill.
2. **Service**: `CategoryInput.Color`/`CategoryService`, `ISettingsService`.
3. **Admin-UI**: Farbwähler in Kategorien, neue Seite `/Admin/Settings`,
   Navigation.
4. **Darstellung**: `CategoryDisplay.Color`, `_ArticleCard`, `site.css`,
   `site.js`, `ArticlesController`-JSON.
5. **Listenlänge**: `IndexModel`, `SucheModel`, `ArticlesController`,
   `Index.cshtml`.
6. **API**: `CategoryWriteModel`/`CategoryReadModel` um `Color`.
7. **Tests**: Farbe, Einstellungen, API.
8. **Doku**: DataSchema, docs/02, docs/03, docs/05, README.
9. **Abschluss**: Build, komplette Testsuite, Smoke-Test (Startseite mit
   Kategorie-Rahmen, Admin-Einstellung).

---

## 8. Entscheidungen und Risiken

- **Ein globaler Wert** für „Beiträge pro Liste“ (Startseite, Suche, REST-Grid).
  Das entspricht der Formulierung „in einer Liste“ und hält die Bedienung einfach.
- **Farbe als Hex-String** (`#RRGGBB`) statt Enum: flexibel, direkt im
  `<input type="color">` nutzbar, keine Migration von Enum-Werten nötig.
- **Fallback-Farbe** `#7f8c8d` (neutrales Grau), damit bestehende Kategorien und
  Artikel ohne Farbe weiterhin sauber aussehen.
- **3-px-Rahmen** ersetzt den bisherigen 1-px-Rahmen; der Hover-Glow bleibt
  erhalten, überschreibt aber nicht mehr die Kategorie-Farbe.
- **Keine Änderung** an der öffentlichen Filter-URL; die Farbe ist rein visuell.
