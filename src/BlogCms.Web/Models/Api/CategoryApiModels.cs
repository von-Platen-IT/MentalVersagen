using BlogCms.Domain.Entities;

namespace BlogCms.Web.Models.Api;

/// <summary>
/// Request zum Anlegen oder Bearbeiten einer Kategorie über die Management-API
/// (Endpunkte <c>createCategory</c> und <c>updateCategory</c>).
/// </summary>
public sealed class CategoryWriteModel
{
    /// <summary>Anzeigename der Kategorie. Pflicht, maximal 100 Zeichen.</summary>
    /// <example>Hintergründe</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-Abschnitt für Filter und Links. Leer lassen, um ihn aus dem Namen zu
    /// generieren. Der Slug <c>undefined</c> ist der Systemkategorie vorbehalten.
    /// </summary>
    /// <example>hintergruende</example>
    public string? Slug { get; set; }

    /// <summary>Optionale Kurzbeschreibung (maximal 500 Zeichen).</summary>
    public string? Description { get; set; }

    /// <summary>Optionaler Disclaimer, der bei Artikeln dieser Kategorie angezeigt wird.</summary>
    public string? Disclaimer { get; set; }

    /// <summary>
    /// Hex-Farbe (<c>#RRGGBB</c>) für den 3-px-Rahmen in Listen. Leer lassen für
    /// die neutrale Standardfarbe <c>#7f8c8d</c>.
    /// </summary>
    /// <example>#b03a2e</example>
    public string? Color { get; set; }

    /// <summary>Sortierreihenfolge in Listen (kleiner Wert zuerst).</summary>
    /// <example>10</example>
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Darstellung einer Kategorie in API-Antworten (Endpunkte <c>listCategories</c>,
/// <c>getCategory</c>, <c>createCategory</c>, <c>updateCategory</c>).
/// </summary>
public sealed class CategoryReadModel
{
    /// <summary>Stabile UUID der Kategorie.</summary>
    public Guid Id { get; set; }

    /// <summary>URL-Abschnitt für Filter und Links.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Anzeigename der Kategorie.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Disclaimer { get; set; }

    /// <summary>Hex-Farbe (<c>#RRGGBB</c>) für den Rahmen in Listen.</summary>
    public string Color { get; set; } = "#7f8c8d";

    public int DisplayOrder { get; set; }

    /// <summary>True für die geschützte Systemkategorie <c>undefined</c>.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Anzahl der Artikel, die dieser Kategorie zugeordnet sind.</summary>
    public int ArticleCount { get; set; }

    /// <summary>Erzeugt das Lesemodell aus einer Kategorie-Entität.</summary>
    public static CategoryReadModel FromEntity(Category category, int articleCount = 0) => new()
    {
        Id = category.Id,
        Slug = category.Slug,
        Name = category.Name,
        Description = category.Description,
        Disclaimer = category.Disclaimer,
        Color = category.Color,
        DisplayOrder = category.DisplayOrder,
        IsSystem = category.IsSystem,
        ArticleCount = articleCount
    };
}
