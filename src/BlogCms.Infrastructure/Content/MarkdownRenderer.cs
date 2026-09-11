using Ganss.Xss;
using Markdig;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Renders article Markdown to HTML for display.
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Converts Markdown to HTML and sanitizes the result, so that no raw HTML
    /// or script injection can make it through (see 01-Content-Verwaltung.md).
    /// </summary>
    string ToSafeHtml(string? markdown);
}

/// <summary>
/// Markdig-based renderer with mandatory HTML sanitizing via HtmlSanitizer.
/// </summary>
public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _sanitizer = new HtmlSanitizer();

        // Only safe link schemes survive; javascript:/data: URLs are dropped.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
    }

    public string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(html);
    }
}
