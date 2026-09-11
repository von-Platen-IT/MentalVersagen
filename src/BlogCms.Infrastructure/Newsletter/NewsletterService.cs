using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Newsletter;

public sealed class NewsletterOptions
{
    public const string SectionName = "Newsletter";

    /// <summary>Unconfirmed sign-ups are deleted after this many days (double opt-in).</summary>
    public int UnconfirmedRetentionDays { get; set; } = 7;
}

public sealed record NewsletterSubscribeResult(NewsletterSubscriber Subscriber, string Token, bool AlreadyConfirmed);

public interface INewsletterService
{
    Task<NewsletterSubscribeResult> SubscribeAsync(
        string email, Guid? userId, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(string token, CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredUnconfirmedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Newsletter double opt-in handling. The confirmation/unsubscribe token is a
/// protected (signed + encrypted) subscriber id, so no schema change is needed
/// and the link works without a login (see 04-Monetarisierung.md).
/// </summary>
public sealed class NewsletterService : INewsletterService
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly NewsletterOptions _options;

    public NewsletterService(
        AppDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        Microsoft.Extensions.Options.IOptions<NewsletterOptions> options)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("BlogCms.Newsletter.Token");
        _options = options.Value;
    }

    public async Task<NewsletterSubscribeResult> SubscribeAsync(
        string email, Guid? userId, CancellationToken cancellationToken = default)
    {
        email = (email ?? string.Empty).Trim().ToLowerInvariant();

        var subscriber = await _db.NewsletterSubscribers
            .FirstOrDefaultAsync(s => s.Email == email, cancellationToken);

        var alreadyConfirmed = subscriber?.ConfirmedAt is not null;

        if (subscriber is null)
        {
            subscriber = new NewsletterSubscriber
            {
                Email = email,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _db.NewsletterSubscribers.Add(subscriber);
        }
        else if (subscriber.UnsubscribedAt is not null)
        {
            // Re-signup after unsubscribe starts the double opt-in again.
            subscriber.UnsubscribedAt = null;
            subscriber.ConfirmedAt = null;
        }

        if (userId is not null)
        {
            subscriber.UserId ??= userId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var token = _protector.Protect(subscriber.Id.ToString());
        return new NewsletterSubscribeResult(subscriber, token, alreadyConfirmed);
    }

    public async Task<bool> ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        var id = Unprotect(token);
        if (id is null)
        {
            return false;
        }

        var subscriber = await _db.NewsletterSubscribers
            .FirstOrDefaultAsync(s => s.Id == id.Value, cancellationToken);

        if (subscriber is null)
        {
            return false;
        }

        subscriber.ConfirmedAt = DateTime.UtcNow;
        subscriber.UnsubscribedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default)
    {
        var id = Unprotect(token);
        if (id is null)
        {
            return false;
        }

        var subscriber = await _db.NewsletterSubscribers
            .FirstOrDefaultAsync(s => s.Id == id.Value, cancellationToken);

        if (subscriber is null)
        {
            return false;
        }

        // No hard delete: re-signup is handled cleanly.
        subscriber.UnsubscribedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteExpiredUnconfirmedAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.UnconfirmedRetentionDays);

        var expired = await _db.NewsletterSubscribers
            .Where(s => s.ConfirmedAt == null && s.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        _db.NewsletterSubscribers.RemoveRange(expired);
        await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private Guid? Unprotect(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var unprotected = _protector.Unprotect(token);
            return Guid.TryParse(unprotected, out var id) ? id : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
