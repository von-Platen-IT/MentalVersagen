using BlogCms.Web.Configuration;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Extensions;

/// <summary>
/// Helpers for reading request metadata in the UI layer (where the HTTP context
/// is available). Domain/infrastructure services never see the request.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the client IP address. Prefers the first <c>X-Forwarded-For</c>
    /// entry (reverse proxy) and falls back to the socket address. May be
    /// <c>null</c> when the address cannot be determined.
    /// </summary>
    public static string? GetClientIp(this HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0)
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Öffentliche Basis-URL der Anwendung ohne Trailing-Slash, z. B.
    /// "https://mentalversagen.de". Basis aller absoluten URLs, die nach außen
    /// gehen (RSS-Feed, Bestätigungs- und Abmelde-Links in E-Mails).
    ///
    /// Reihenfolge:
    /// 1. <see cref="SiteOptions.PublicBaseUrl"/>, wenn konfiguriert — die
    ///    zuverlässigste Variante, unabhängig von Proxy und Host-Header.
    /// 2. sonst Schema + Host des Requests. Hinter einem Reverse Proxy ist das
    ///    nur korrekt, wenn die Forwarded-Header ausgewertet werden
    ///    (<c>app.UseForwardedHeaders()</c>, siehe <see cref="SiteOptions"/>).
    /// </summary>
    public static string GetPublicBaseUrl(this HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<SiteOptions>>()?.Value;

        if (options is not null && options.HasPublicBaseUrl)
        {
            return options.NormalizedPublicBaseUrl;
        }

        return $"{context.Request.Scheme}://{context.Request.Host}".TrimEnd('/');
    }
}
