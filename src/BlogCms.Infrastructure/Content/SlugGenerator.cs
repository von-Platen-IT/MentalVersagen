using System.Globalization;
using System.Text;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Produces URL-friendly slugs from article/tag titles.
/// </summary>
public interface ISlugGenerator
{
    string Generate(string? input);
}

/// <summary>
/// Generates lowercase, dash-separated slugs and transliterates German umlauts
/// (ä→ae, ö→oe, ü→ue, ß→ss) so URLs stay readable.
/// </summary>
public sealed class SlugGenerator : ISlugGenerator
{
    public string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        // Decompose accents (é -> e) and drop combining marks.
        normalized = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '.' or '/')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString();

        // Collapse repeated dashes and trim them from both ends.
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }
}
