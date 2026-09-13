namespace BlogCms.Domain.Entities;

/// <summary>
/// A single article reference within a <see cref="LinkList"/>, ordered by
/// <see cref="Position"/> independently of the publishing date (FeatureFix1 BR-091).
/// </summary>
public class LinkListItem : EntityBase
{
    public Guid LinkListId { get; set; }
    public LinkList? LinkList { get; set; }

    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Editorial ordering within the list.</summary>
    public int Position { get; set; }
}
