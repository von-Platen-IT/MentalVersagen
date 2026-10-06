using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// An uploaded image (title image or in-content image) as an open stream plus its
/// original file name. The Web layer opens the streams; this layer stays free of
/// ASP.NET Core types (no <c>IFormFile</c> dependency).
/// </summary>
public sealed record ArticleUpload(Stream Content, string FileName);

/// <summary>
/// Application-layer description of a posted article (create or edit). Carries all
/// form data in a presentation-agnostic shape; the Web layer maps its input model
/// onto it. Field names used by <see cref="PostError"/> match the Web input model
/// properties so errors can be bound to form fields directly.
/// </summary>
public sealed record ArticlePostRequest(
    string Title,
    string? Slug,
    string? TitleImageUrl,
    string Excerpt,
    string ContentMarkdown,
    ArticleCategory Category,
    ArticleAccessLevel AccessLevel,
    ArticleStatus Status,
    DateTime? ScheduledAt,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Hashtags,
    string? VideoUrl,
    ArticleUpload? CoverUpload,
    IReadOnlyList<ArticleUpload> ContentUploads,
    IReadOnlyList<Guid> UploadedImageIds)
{
    /// <summary>
    /// Validates that a title image source is present: either an external URL, a new
    /// upload, or (on edit) an already stored image (FeatureFix1 BR-022).
    /// </summary>
    public bool HasTitleImageSource(bool hasExistingImage) =>
        !string.IsNullOrWhiteSpace(TitleImageUrl)
        || CoverUpload is not null
        || hasExistingImage
        || UploadedImageIds.Count > 0;

    public static IReadOnlyList<string> ParseTags(string? value) => ParseList(value);

    /// <summary>
    /// Splits hashtag input. A leading <c>#</c> is intentionally NOT trimmed here —
    /// the persistence boundary (<c>ArticleService.SyncHashtagsAsync</c>) is the
    /// single place that normalizes hashtags.
    /// </summary>
    public static IReadOnlyList<string> ParseHashtags(string? value) => ParseList(value);

    /// <summary>Parses comma-separated editor image ids into distinct GUIDs.</summary>
    public static IReadOnlyList<Guid> ParseUploadedImageIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// A single validation/processing error. <see cref="Field"/> is either one of the
/// constants below (matching the Web input model property names) or
/// <see cref="GenericField"/> for form-level errors.
/// </summary>
public sealed record PostError(string Field, string Message)
{
    public const string GenericField = "";
    public const string ImageUploadField = "ImageUpload";
    public const string ContentUploadsField = "ContentImageUploads";
    public const string ScheduledAtField = "ScheduledAt";
}

/// <summary>Outcome of a post operation: the saved article or a list of field errors.</summary>
public sealed record PostResult(bool Succeeded, Article? Article, IReadOnlyList<PostError> Errors)
{
    public static PostResult Ok(Article article) => new(true, article, []);

    public static PostResult Fail(params PostError[] errors) => new(false, null, errors);
}