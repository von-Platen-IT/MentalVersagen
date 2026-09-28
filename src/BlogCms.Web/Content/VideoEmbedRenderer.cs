using System.Net;
using System.Text.RegularExpressions;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Web.Content;

/// <summary>
/// Renders a <see cref="VideoEmbed"/> safely. Known platforms are embedded in a
/// direct iframe whose URL is built server-side from a strictly validated video id
/// (no user-supplied HTML is injected). Unknown platforms and URLs without a
/// recognizable id fall back to a plain link
/// (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public static class VideoEmbedRenderer
{
    // YouTube video ids are exactly 11 characters of [A-Za-z0-9_-].
    private static readonly Regex YouTubeIdRegex = new(
        @"(?:youtu\.be/|youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|v/|live/))([A-Za-z0-9_-]{11})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Vimeo ids are numeric (optionally prefixed with /video/).
    private static readonly Regex VimeoIdRegex = new(
        @"vimeo\.com/(?:video/)?(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Render(VideoEmbed embed)
    {
        var embedUrl = ResolveEmbedUrl(embed.Platform, embed.OriginalUrl);

        if (embedUrl is null)
        {
            return RenderLink(embed.OriginalUrl);
        }

        var safeUrl = WebUtility.HtmlEncode(embedUrl);

        // The iframe URL is constructed from a validated id, so no untrusted HTML is
        // injected. The sandbox keeps allow-same-origin so the cross-origin player can
        // retain its own origin (required for playback); allow-scripts is needed by the
        // player itself. This is the documented working configuration for YouTube/Vimeo.
        return
            $"""
            <div class="ratio ratio-16x9 my-3">
                <iframe src="{safeUrl}" loading="lazy" title="Externes Video"
                        sandbox="allow-scripts allow-same-origin allow-popups allow-presentation"
                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                        referrerpolicy="strict-origin-when-cross-origin"
                        allowfullscreen style="border:0;width:100%;height:100%"></iframe>
            </div>
            """;
    }

    /// <summary>
    /// Builds a trusted embed URL from the original URL by extracting a validated
    /// video id. Returns <c>null</c> when the platform cannot be embedded safely
    /// (e.g. X/TikTok, which deliver blockquote+script markup).
    /// </summary>
    public static string? ResolveEmbedUrl(VideoPlatform platform, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return platform switch
        {
            VideoPlatform.YouTube => ResolveYouTube(url),
            VideoPlatform.Vimeo => ResolveVimeo(url),
            _ => null
        };
    }

    private static string? ResolveYouTube(string url)
    {
        var match = YouTubeIdRegex.Match(url);
        return match.Success
            ? $"https://www.youtube-nocookie.com/embed/{match.Groups[1].Value}"
            : null;
    }

    private static string? ResolveVimeo(string url)
    {
        var match = VimeoIdRegex.Match(url);
        return match.Success
            ? $"https://player.vimeo.com/video/{match.Groups[1].Value}"
            : null;
    }

    private static string RenderLink(string url)
    {
        var link = WebUtility.HtmlEncode(url);
        return $"<p><a href=\"{link}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{link}</a></p>";
    }
}
