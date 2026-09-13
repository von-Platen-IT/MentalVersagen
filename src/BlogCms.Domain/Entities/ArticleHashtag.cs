namespace BlogCms.Domain.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between articles and hashtags.
/// </summary>
public class ArticleHashtag
{
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    public Guid HashtagId { get; set; }
    public Hashtag? Hashtag { get; set; }
}
