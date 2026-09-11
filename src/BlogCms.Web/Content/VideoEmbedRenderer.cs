using System.Net;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Web.Content;

/// <summary>
/// Renders a <see cref="VideoEmbed"/> safely. Known platforms are embedded in a
/// sandboxed iframe using only the official oEmbed code; unknown platforms and
/// failed lookups fall back to a plain link
/// (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public static class VideoEmbedRenderer
{
    public static string Render(VideoEmbed embed)
    {
        if (embed.Platform == VideoPlatform.Other || string.IsNullOrWhiteSpace(embed.EmbedHtml))
        {
            var link = WebUtility.HtmlEncode(embed.OriginalUrl);
            return $"<p><a href=\"{link}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{link}</a></p>";
        }

        // The sandbox intentionally omits allow-same-origin to reduce XSS risk
        // from user-generated embed content.
        var srcdoc = WebUtility.HtmlEncode(embed.EmbedHtml);

        return
            $"""
            <div class="ratio ratio-16x9 my-3">
                <iframe sandbox="allow-scripts allow-popups" srcdoc="{srcdoc}" loading="lazy" title="Externes Video" style="border:0;width:100%;height:100%"></iframe>
            </div>
            """;
    }
}
