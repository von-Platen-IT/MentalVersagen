namespace BlogCms.Domain.Entities;

/// <summary>
/// Free-form tag for granular filtering of articles.
/// </summary>
public class Tag : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    // Relationships
    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
}
