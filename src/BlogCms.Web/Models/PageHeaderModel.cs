namespace BlogCms.Web.Models;

/// <summary>
/// Parameter for the shared page header partial (`_PageHeader.cshtml`).
/// <description>
/// Choose one of the three header slide backgrounds via <see cref="Slide"/> (1–3).
/// Title/Meta may each be rendered (optional).
/// </description>
/// </summary>
public sealed class PageHeaderModel
{
    public int Slide { get; set; } = 1;

    public string Title { get; set; } = string.Empty;

    public string? Sub { get; set; }

    public string? Meta { get; set; }
}
