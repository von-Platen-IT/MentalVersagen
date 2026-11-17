# Plan: Ursprung eines Beitrags (Mensch / KI / Hybrid)

## 1. Ziel

Beiträge erhalten eine **Ursprungs-Kennzeichnung**:

1. In Listen ist über ein **kleines SVG-Icon** sofort erkennbar, ob ein Beitrag
   von einem **Menschen**, von **KI** oder **hybrid** erstellt wurde.
2. Beim **Hover** über das Icon erscheint ein erklärender Text (Tooltip).
3. Der Ursprung ist über die **Management-API** pflegbar (lesen und schreiben).
4. Zusätzlich ist der Ursprung im **Admin-Formular** wählbar.

Keine Änderung an der bestehenden KI-Kennzeichnung (`_KiKennzeichnung`) — diese
bleibt der rechtliche Hinweis; das neue Icon ist die redaktionelle Einordnung.

---

## 2. Ausgangslage

| Bereich | Datei | Ist-Zustand |
|---|---|---|
| Artikel-Entität | [`Article.cs`](src/BlogCms.Domain/Entities/Article.cs) | Kein Ursprungsfeld. |
| Artikel-Konfiguration | [`ArticleConfiguration.cs`](src/BlogCms.Infrastructure/Data/Configurations/ArticleConfiguration.cs) | Keine Ursprungs-Spalte. |
| Post-Request | [`ArticlePostRequest.cs`](src/BlogCms.Infrastructure/Content/ArticlePostRequest.cs) | Kein Ursprung. |
| Posting-Service | [`ArticlePostingService.cs`](src/BlogCms.Infrastructure/Content/ArticlePostingService.cs) | Setzt kein Ursprungsfeld. |
| Admin-Formular | [`_ArticleForm.cshtml`](src/BlogCms.Web/Pages/Admin/Articles/_ArticleForm.cshtml) | Keine Ursprungs-Auswahl. |
| Karten-Partial | [`_ArticleCard.cshtml`](src/BlogCms.Web/Pages/Shared/_ArticleCard.cshtml) | Kein Ursprungs-Icon. |
| JS-Karten | [`site.js`](src/BlogCms.Web/wwwroot/js/site.js:309) | Kein Ursprungs-Icon. |
| API-Modelle | [`ArticleApiModels.cs`](src/BlogCms.Web/Models/Api/ArticleApiModels.cs) | Kein Ursprungsfeld. |
| Bestehende KI-Kennzeichnung | [`_KiKennzeichnung.cshtml`](src/BlogCms.Web/Pages/Shared/_KiKennzeichnung.cshtml) | Rechtlicher Hinweis, unverändert. |

---

## 3. Datenmodell

### 3.1 Enum

Neue Datei `src/BlogCms.Domain/Enums/ArticleOrigin.cs`:
```csharp
public enum ArticleOrigin
{
    Human = 0,
    Ai = 1,
    Hybrid = 2
}
```

### 3.2 Entität

`Article` erhält:
```csharp
/// <summary>Editorial origin: human, AI-generated or hybrid.</summary>
public ArticleOrigin Origin { get; set; } = ArticleOrigin.Human;
```

`ArticleConfiguration`:
```csharp
builder.Property(a => a.Origin)
    .IsRequired()
    .HasDefaultValue(ArticleOrigin.Human);
```

### 3.3 Migration

`AddArticleOrigin`:
- `AddColumn<string>("Origin", "Articles", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Human")`.
- `Down`: Spalte entfernen.

Da Enums relational als String gespeichert werden (bestehende Konvention in
`AppDbContext`), lautet der Default `"Human"`.

---

## 4. Anwendungsschicht

### 4.1 `ArticlePostRequest`

Neuer optionaler Parameter am Ende:
```csharp
ArticleOrigin Origin = ArticleOrigin.Human
```
Bestehende Aufrufe bleiben kompatibel.

### 4.2 `ArticlePostingService`

- `CreateAsync`: `Origin = request.Origin` im `new Article { … }`.
- `UpdateAsync`: `article.Origin = request.Origin`.

### 4.3 Anzeige-Helfer

Neue Datei `src/BlogCms.Web/Content/OriginDisplay.cs`:
```csharp
public static class OriginDisplay
{
    public static string Label(ArticleOrigin origin) => origin switch
    {
        ArticleOrigin.Ai => "KI",
        ArticleOrigin.Hybrid => "Hybrid",
        _ => "Mensch"
    };

    public static string Description(ArticleOrigin origin) => origin switch
    {
        ArticleOrigin.Ai => "Mit künstlicher Intelligenz erstellt",
        ArticleOrigin.Hybrid => "Mensch und KI (Hybrid)",
        _ => "Von einem Menschen erstellt"
    };
}
```

---

## 5. Admin-Formular

- [`ArticleInputModel`](src/BlogCms.Web/Models/ArticleInputModel.cs):
  ```csharp
  [Display(Name = "Ursprung")]
  public ArticleOrigin Origin { get; set; } = ArticleOrigin.Human;
  ```
  `ToRequest` übergibt `Origin`.
- [`_ArticleForm.cshtml`](src/BlogCms.Web/Pages/Admin/Articles/_ArticleForm.cshtml):
  Im Abschnitt „Einordnung & Themengebiet“ ein Select:
  ```html
  <select asp-for="Origin" class="form-select"
          asp-items="Html.GetEnumSelectList<ArticleOrigin>()"></select>
  ```
- [`Admin/Articles/Edit.cshtml.cs`](src/BlogCms.Web/Pages/Admin/Articles/Edit.cshtml.cs):
  Beim Laden `Origin = article.Origin` in das InputModel übernehmen.

---

## 6. Darstellung in Listen

### 6.1 Partial `_OriginBadge.cshtml`

Neue Datei `src/BlogCms.Web/Pages/Shared/_OriginBadge.cshtml`:
```html
@model BlogCms.Domain.Enums.ArticleOrigin
@using BlogCms.Web.Content
@{
    var label = OriginDisplay.Label(Model);
    var description = OriginDisplay.Description(Model);
}
<span class="mv-origin-badge mv-origin-badge--@Model.ToString().ToLowerInvariant()"
      title="@description" aria-label="@description" role="img">
    @if (Model == ArticleOrigin.Ai)
    {
        <!-- Roboter/Sparkles -->
    }
    else if (Model == ArticleOrigin.Hybrid)
    {
        <!-- Person + Sparkles -->
    }
    else
    {
        <!-- Person -->
    }
</span>
```

Die SVGs sind klein (16×16), `fill="none" stroke="currentColor"` und erben die
Textfarbe. Der `title` liefert den Hover-Text; `aria-label` macht ihn für
Screenreader verfügbar.

### 6.2 Karten-Partial

[`_ArticleCard.cshtml`](src/BlogCms.Web/Pages/Shared/_ArticleCard.cshtml:25):
Im `mv-case__head` neben dem Kategorie-Label:
```html
<span class="mv-case__origin">
    <partial name="_OriginBadge" model="article.Origin" />
</span>
```

### 6.3 JS-Karten

[`site.js`](src/BlogCms.Web/wwwroot/js/site.js:309) `renderCard`:
- `article.origin` und `article.originDescription` aus der API nutzen.
- Ein `<span class="mv-origin-badge …">` mit dem passenden SVG und `title`
  erzeugen und in den `mv-case__head` einfügen.

### 6.4 CSS

In [`site.css`](src/BlogCms.Web/wwwroot/css/site.css) ergänzen:
```css
.mv-origin-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.1rem;
  height: 1.1rem;
  color: var(--mv-text-dim);
  cursor: help;
}
.mv-origin-badge--ai { color: #6c8ebf; }
.mv-origin-badge--hybrid { color: #b7950b; }
.mv-origin-badge--human { color: var(--mv-text-dim); }
```

---

## 7. Management-API

### 7.1 Modelle

- `ArticleWriteModel`:
  ```csharp
  /// <summary>Ursprung: Human, Ai oder Hybrid. Standard: Human.</summary>
  public ArticleOrigin Origin { get; set; } = ArticleOrigin.Human;
  ```
- `ArticleReadModel`:
  ```csharp
  public ArticleOrigin Origin { get; set; }
  public string OriginLabel { get; set; } = "Mensch";
  ```
  `FromEntity` setzt `Origin = article.Origin` und
  `OriginLabel = OriginDisplay.Label(article.Origin)`.

### 7.2 Controller

[`ManagementArticlesController.ToPostRequest`](src/BlogCms.Web/Controllers/ManagementArticlesController.cs:297)
übergibt `Origin: model.Origin`.

### 7.3 Öffentliches REST-Grid

[`ArticlesController`](src/BlogCms.Web/Controllers/ArticlesController.cs:71) JSON
um `origin` (String) und `originDescription` erweitern.

---

## 8. Tests

| Testdatei | Inhalt |
|---|---|
| `ArticlePostingTests.cs` (erweitert) | `CreateAsync` speichert `Origin`; `UpdateAsync` ändert `Origin` |
| `ManagementArticlesControllerTests.cs` (erweitert) | `createArticle`/`updateArticle` mit `origin`; `getArticle` liefert `origin` und `originLabel` |
| `ContentTests.cs` (erweitert) | Default ist `Human` |

Ziel: bestehende 299 Tests bleiben grün, neue Tests decken den Ursprung ab.

---

## 9. Dokumentation

- [`DataSchema.md`](DataSchema.md): `Article.Origin` und Enum `ArticleOrigin`.
- [`docs/02-admin-artikelverwaltung.md`](docs/02-admin-artikelverwaltung.md):
  Ursprungs-Auswahl im Formular.
- [`docs/03-blog-nutzen.md`](docs/03-blog-nutzen.md): Icon und Hover-Text in
  Listen.
- [`docs/05-management-api.md`](docs/05-management-api.md): `origin` im
  Write-/Read-Modell mit Beispiel.
- [`README.md`](README.md): kurzer Absatz zur Ursprungs-Kennzeichnung.

---

## 10. Umsetzungsreihenfolge

1. **Datenmodell + Migration**: `ArticleOrigin`, `Article.Origin`,
   `ArticleConfiguration`, Migration.
2. **Anwendungsschicht**: `ArticlePostRequest`, `ArticlePostingService`,
   `OriginDisplay`.
3. **Admin-Formular**: `ArticleInputModel`, `_ArticleForm`, `Edit.cshtml.cs`.
4. **Darstellung**: `_OriginBadge`, `_ArticleCard`, `site.js`, `site.css`.
5. **API**: `ArticleWriteModel`, `ArticleReadModel`, Controller, öffentliches
   REST-Grid.
6. **Tests**: Posting, API, Default.
7. **Doku**: DataSchema, docs/02, docs/03, docs/05, README.
8. **Abschluss**: Build, komplette Testsuite, Smoke-Test (Icon im HTML,
   `origin` in der API).

---

## 11. Entscheidungen und Risiken

- **Enum statt Freitext**: feste Werte `Human`, `Ai`, `Hybrid` verhindern
  Tippfehler und machen die API stabil.
- **Default `Human`**: bestehende Artikel werden bei der Migration als
  menschlich eingeordnet; das ist die neutralste Annahme.
- **Icon + Tooltip**: Das Icon ist rein visuell; der `title`/`aria-label` liefert
  den Text beim Hover und für Screenreader.
- **Bestehende KI-Kennzeichnung bleibt unverändert**: Sie erfüllt die rechtliche
  Hinweispflicht; das neue Icon ist eine zusätzliche redaktionelle Information.
- **Keine Änderung an Filter-URLs**: Der Ursprung ist zunächst nur Anzeige und
  API-Feld, kein Filter.
