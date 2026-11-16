using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;

namespace BlogCms.Web.Content;

/// <summary>
/// Presentation helpers for the dynamic category catalogue. Keep all display
/// fallbacks in one place so an article without a loaded category still renders
/// the system label <c>undefined</c>.
/// </summary>
public static class CategoryDisplay
{
    public const string UndefinedName = "undefined";

    public static string Name(Category? category) =>
        string.IsNullOrWhiteSpace(category?.Name) ? UndefinedName : category!.Name;

    public static string Slug(Category? category) =>
        string.IsNullOrWhiteSpace(category?.Slug) ? CategoryIds.UndefinedSlug : category!.Slug;

    /// <summary>Optional hint text configured per category (e.g. satire disclaimer).</summary>
    public static string? Disclaimer(Category? category) =>
        string.IsNullOrWhiteSpace(category?.Disclaimer) ? null : category!.Disclaimer;

    public static string BadgeClass(Category? category) =>
        category?.IsSystem == true ? "bg-secondary" : "bg-primary";
}
