using BlogCms.Infrastructure.Contact;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BlogCms.Web.Security;

/// <summary>
/// Limits how many contact messages a single client may send in a short
/// window. Together with the captcha and the honeypot this keeps the contact
/// form from being abused as a mail relay.
/// </summary>
public interface IContactThrottle
{
    /// <summary>
    /// Registers an attempt for the given client key (usually the IP address).
    /// Returns <c>false</c> when the limit is exhausted.
    /// </summary>
    bool TryAcquire(string? clientKey);
}

/// <summary>
/// In-memory implementation backed by <see cref="IMemoryCache"/>. The window
/// slides with every attempt, so a blocked client has to wait a full window
/// after their last try.
/// </summary>
public sealed class MemoryCacheContactThrottle : IContactThrottle
{
    private readonly IMemoryCache _cache;
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;

    public MemoryCacheContactThrottle(IMemoryCache cache, IOptions<ContactOptions> options)
    {
        _cache = cache;
        _maxPerWindow = Math.Max(1, options.Value.MaxPerWindow);
        _window = TimeSpan.FromMinutes(Math.Max(1, options.Value.WindowMinutes));
    }

    public bool TryAcquire(string? clientKey)
    {
        var key = $"contact-throttle:{clientKey ?? "unknown"}";
        var attempts = _cache.Get<int?>(key) ?? 0;

        if (attempts >= _maxPerWindow)
        {
            return false;
        }

        _cache.Set(key, attempts + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _window
        });

        return true;
    }
}
