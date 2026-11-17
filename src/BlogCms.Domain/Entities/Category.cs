namespace BlogCms.Domain.Entities;

/// <summary>
/// Editorial category / topic area. Categories are managed by administrators at
/// runtime. The system category <c>undefined</c> is the default for articles that
/// have no explicit category and cannot be deleted or renamed.
/// </summary>
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable URL/filter segment; does not change when the name is edited.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional internal description shown in the admin category list.</summary>
    public string? Description { get; set; }

    /// <summary>Optional hint shown on the article page (e.g. satire disclaimer).</summary>
    public string? Disclaimer { get; set; }

    /// <summary>
    /// Hex colour (<c>#RRGGBB</c>) used for the 3 px frame around article cards in
    /// lists. Administrators can change it per category.
    /// </summary>
    public string Color { get; set; } = "#7f8c8d";

    /// <summary>Sort order in selection lists (lower values first).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// <c>true</c> only for the built-in <c>undefined</c> category. System
    /// categories cannot be deleted or renamed.
    /// </summary>
    public bool IsSystem { get; set; }

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}