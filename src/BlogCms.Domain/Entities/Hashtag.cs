namespace BlogCms.Domain.Entities;

/// <summary>
/// A free-form hashtag (FeatureFix1 BR-026), tracked separately from <see cref="Tag"/>
/// so it can be searched and filtered on its own.
/// </summary>
public class Hashtag : EntityBase
{
    /// <summary>Name without a leading '#'.</summary>
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    // Relationships
    public ICollection<ArticleHashtag> ArticleHashtags { get; set; } = new List<ArticleHashtag>();
}
