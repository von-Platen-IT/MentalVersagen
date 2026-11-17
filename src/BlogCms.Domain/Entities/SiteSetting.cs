namespace BlogCms.Domain.Entities;

/// <summary>
/// Runtime-editable site setting stored as a key/value pair. Used for display
/// options that administrators can change without a redeploy (for example the
/// number of articles shown per list).
/// </summary>
public class SiteSetting
{
    /// <summary>Stable setting key, e.g. <c>Display:ArticlesPerPage</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Serialized value; integers are stored as invariant strings.</summary>
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
