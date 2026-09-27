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
}
