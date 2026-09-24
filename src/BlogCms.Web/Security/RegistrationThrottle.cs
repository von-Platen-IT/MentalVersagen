using Microsoft.Extensions.Caching.Memory;

namespace BlogCms.Web.Security;

/// <summary>
/// Limits how many registration attempts a single client may make in a short
/// window. This is the most effective protection against mass registrations:
/// bots can solve or skip a captcha, but they cannot avoid the request limit.
/// </summary>
public interface IRegistrationThrottle
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
public sealed class MemoryCacheRegistrationThrottle : IRegistrationThrottle
{
    /// <summary>Maximum attempts per window and client.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Length of the sliding window.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;

    public MemoryCacheRegistrationThrottle(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryAcquire(string? clientKey)
    {
        var key = $"registration-throttle:{clientKey ?? "unknown"}";
        var attempts = _cache.Get<int?>(key) ?? 0;

        if (attempts >= MaxAttempts)
        {
            return false;
        }

        _cache.Set(key, attempts + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Window
        });

        return true;
    }
}
