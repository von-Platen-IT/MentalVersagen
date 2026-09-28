namespace BlogCms.Domain.Entities;

/// <summary>
/// Editorial category / Themengebiet for blog articles.
/// Stored in a dedicated database table (FeatureFix1 BR-025).
/// </summary>
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique URL segment for filtering.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
