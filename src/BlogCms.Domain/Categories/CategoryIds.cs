namespace BlogCms.Domain.Categories;

/// <summary>
/// Stable identifiers for the built-in categories. The fixed GUIDs are required
/// so that the database migration can map the former <c>ArticleCategory</c> enum
/// values deterministically and so that tests remain reproducible.
/// </summary>
public static class CategoryIds
{
    public static readonly Guid Undefined = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Politik = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Satire = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Verschwoerungstheorien = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public const string UndefinedSlug = "undefined";
    public const string PolitikSlug = "politik";
    public const string SatireSlug = "satire";
    public const string VerschwoerungstheorienSlug = "verschwoerungstheorien";

    /// <summary>Default category id used when an article is saved without a category.</summary>
    public static Guid Default => Undefined;
}