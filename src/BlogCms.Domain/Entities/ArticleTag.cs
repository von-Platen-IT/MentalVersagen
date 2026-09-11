namespace BlogCms.Domain.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between articles and tags.
/// </summary>
public class ArticleTag
{
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }
}
