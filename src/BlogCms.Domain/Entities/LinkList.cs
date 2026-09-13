namespace BlogCms.Domain.Entities;

/// <summary>
/// An editorially curated, ordered list of articles (FeatureFix1 BR-090/BR-091).
/// Managed by the administrator and embeddable on other pages.
/// </summary>
public class LinkList : EntityBase
{
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Relationships
    public ICollection<LinkListItem> Items { get; set; } = new List<LinkListItem>();
}
