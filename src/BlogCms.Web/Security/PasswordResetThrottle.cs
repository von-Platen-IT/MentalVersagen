using Microsoft.Extensions.Caching.Memory;

namespace BlogCms.Web.Security;

/// <summary>
/// Limits how many password-reset requests a single client may make in a short
/// window. This prevents using the "forgot password" form to flood a mailbox or
/// to probe the user database at high speed.
/// </summary>
public interface IPasswordResetThrottle
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
public sealed class MemoryCachePasswordResetThrottle : IPasswordResetThrottle
{
    /// <summary>Maximum attempts per window and client.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Length of the sliding window.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;

    public MemoryCachePasswordResetThrottle(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryAcquire(string? clientKey)
    {
        var key = $"password-reset-throttle:{clientKey ?? "unknown"}";
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
