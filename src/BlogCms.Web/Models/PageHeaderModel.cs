namespace BlogCms.Web.Models;

/// <summary>
/// Parameter for the shared page header partial (`_PageHeader.cshtml`).
/// <description>
/// The header background is chosen randomly per page view from the motif pool
/// (<see cref="BlogCms.Web.Content.PageBackgrounds"/>), so every page shows a
/// different image. Set <see cref="Slide"/> to 1–3 to pin one of the classic
/// header slides; 0 (default) means "random".
/// Title/Meta may each be rendered (optional).
/// </description>
/// </summary>
public sealed class PageHeaderModel
{
    /// <summary>
    /// Feste Header-Slide (1–3) oder 0 (Vorgabe) für eine zufällige Auswahl
    /// aus dem Motiv-Pool.
    /// </summary>
    public int Slide { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Sub { get; set; }

    public string? Meta { get; set; }
}
