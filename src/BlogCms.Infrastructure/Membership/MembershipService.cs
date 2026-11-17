using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Membership;

/// <summary>Filter for the admin membership overview.</summary>
public enum MembershipFilter
{
    All,
    Active,
    PastDue,
    Canceled,
    Complimentary,
    Basic
}

/// <summary>Result of a membership mutation.</summary>
public sealed record MembershipResult(bool Succeeded, MembershipProfile? Profile, string? Error)
{
    public static MembershipResult Ok(MembershipProfile profile) => new(true, profile, null);

    public static MembershipResult Fail(string error) => new(false, null, error);
}

/// <summary>A user together with their optional membership profile.</summary>
public sealed record MembershipAdminRow(User User, MembershipProfile? Profile);

/// <summary>Stripe state to apply to a membership profile.</summary>
public sealed record MembershipStripeSnapshot(
    Guid UserId,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    MembershipStatus Status,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? LastSyncedAt,
    string? PlanId = null);

/// <summary>
/// Access rules for the new Stripe membership feature. The service is only
/// consulted when <see cref="MembershipOptions.Enabled"/> is true; otherwise the
/// existing blog behaviour remains unchanged.
/// </summary>
public interface IMembershipService
{
    Task<MembershipProfile?> GetProfileAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasFullAccessAsync(
        User? user, DateTime now, CancellationToken cancellationToken = default);

    Task<bool> IsArticleFullyVisibleAsync(
        Article article, User? user, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>When a premium article becomes free for everyone; null for other levels.</summary>
    DateTime? GetTeaserAvailableAt(Article article);

    Task<MembershipResult> GrantComplimentaryAsync(
        Guid targetUserId, Guid actorId, DateTime? until, string? note,
        CancellationToken cancellationToken = default);

    Task<MembershipResult> RevokeComplimentaryAsync(
        Guid targetUserId, Guid actorId, string? note,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipAdminRow>> GetForAdminAsync(
        string? search, MembershipFilter filter, CancellationToken cancellationToken = default);

    Task<MembershipResult> ApplyStripeSnapshotAsync(
        MembershipStripeSnapshot snapshot, string action, Guid? actorId, string? details,
        CancellationToken cancellationToken = default);
}

public sealed class MembershipService : IMembershipService
{
    private readonly AppDbContext _db;
    private readonly MembershipOptions _options;

    public MembershipService(AppDbContext db, IOptions<MembershipOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public Task<MembershipProfile?> GetProfileAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        _db.MembershipProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task<bool> HasFullAccessAsync(
        User? user, DateTime now, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            // No subscribers existed in the legacy flow, so Premium articles
            // remain admin-only while the new membership feature is off.
            return user?.Role == UserRole.Admin;
        }

        if (user is null)
        {
            return false;
        }

        if (user.Role == UserRole.Admin)
        {
            return true;
        }

        var profile = await _db.MembershipProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        return HasFullAccess(profile, now);
    }

    public async Task<bool> IsArticleFullyVisibleAsync(
        Article article, User? user, DateTime now, CancellationToken cancellationToken = default)
    {
        if (article.PublishedAt is null)
        {
            return false;
        }

        switch (article.AccessLevel)
        {
            case ArticleAccessLevel.Public:
                return true;

            case ArticleAccessLevel.Registered:
                return user is not null;

            case ArticleAccessLevel.Premium:
                if (await HasFullAccessAsync(user, now, cancellationToken))
                {
                    return true;
                }

                // While the feature is off, Premium stays admin-only; the
                // teaser delay must not silently make it public.
                if (!_options.Enabled)
                {
                    return false;
                }

                return now >= AsUtc(article.PublishedAt.Value)
                    .AddDays(_options.TeaserDelayDays);

            default:
                return false;
        }
    }

    public DateTime? GetTeaserAvailableAt(Article article)
    {
        if (!_options.Enabled ||
            article.AccessLevel != ArticleAccessLevel.Premium ||
            article.PublishedAt is null)
        {
            return null;
        }

        return AsUtc(article.PublishedAt.Value).AddDays(_options.TeaserDelayDays);
    }

    public async Task<MembershipResult> GrantComplimentaryAsync(
        Guid targetUserId, Guid actorId, DateTime? until, string? note,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == targetUserId, cancellationToken))
        {
            return MembershipResult.Fail("Der Benutzer wurde nicht gefunden.");
        }

        var profile = await GetOrCreateProfileAsync(targetUserId, cancellationToken);
        profile.IsComplimentary = true;
        profile.ComplimentaryUntil = until is null ? null : AsUtc(until.Value);
        profile.UpdatedAt = DateTime.UtcNow;

        AddAuditLog(targetUserId, actorId, "Grant", BuildDetails(note, until));

        await _db.SaveChangesAsync(cancellationToken);
        return MembershipResult.Ok(profile);
    }

    public async Task<MembershipResult> RevokeComplimentaryAsync(
        Guid targetUserId, Guid actorId, string? note,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.MembershipProfiles
            .FirstOrDefaultAsync(p => p.UserId == targetUserId, cancellationToken);

        if (profile is null)
        {
            return MembershipResult.Fail("Für diesen Benutzer besteht keine Mitgliedschaft.");
        }

        profile.IsComplimentary = false;
        profile.ComplimentaryUntil = null;
        profile.UpdatedAt = DateTime.UtcNow;

        AddAuditLog(targetUserId, actorId, "Revoke", note);

        await _db.SaveChangesAsync(cancellationToken);
        return MembershipResult.Ok(profile);
    }

    public async Task<IReadOnlyList<MembershipAdminRow>> GetForAdminAsync(
        string? search, MembershipFilter filter, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.DisplayName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.UserName != null && u.UserName.ToLower().Contains(term)));
        }

        var rows = await query
            .OrderBy(u => u.DisplayName)
            .Select(u => new MembershipAdminRow(
                u,
                _db.MembershipProfiles.FirstOrDefault(p => p.UserId == u.Id)))
            .ToListAsync(cancellationToken);

        return filter switch
        {
            MembershipFilter.Active => rows
                .Where(r => IsMember(r.Profile, now))
                .ToList(),
            MembershipFilter.PastDue => rows
                .Where(r => r.Profile?.Status == MembershipStatus.PastDue)
                .ToList(),
            MembershipFilter.Canceled => rows
                .Where(r => r.Profile?.Status == MembershipStatus.Canceled)
                .ToList(),
            MembershipFilter.Complimentary => rows
                .Where(r => IsComplimentary(r.Profile, now))
                .ToList(),
            MembershipFilter.Basic => rows
                .Where(r => !IsMember(r.Profile, now) && !IsComplimentary(r.Profile, now))
                .ToList(),
            _ => rows
        };
    }

    public async Task<MembershipResult> ApplyStripeSnapshotAsync(
        MembershipStripeSnapshot snapshot, string action, Guid? actorId, string? details,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetOrCreateProfileAsync(snapshot.UserId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(snapshot.StripeCustomerId))
        {
            profile.StripeCustomerId = snapshot.StripeCustomerId;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.StripeSubscriptionId))
        {
            profile.StripeSubscriptionId = snapshot.StripeSubscriptionId;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.PlanId))
        {
            profile.PlanId = snapshot.PlanId;
        }

        profile.Status = snapshot.Status;
        profile.CurrentPeriodEnd = snapshot.CurrentPeriodEnd is null
            ? profile.CurrentPeriodEnd
            : AsUtc(snapshot.CurrentPeriodEnd.Value);
        profile.CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd;
        profile.LastSyncedAt = snapshot.LastSyncedAt is null
            ? DateTime.UtcNow
            : AsUtc(snapshot.LastSyncedAt.Value);
        profile.UpdatedAt = DateTime.UtcNow;

        AddAuditLog(snapshot.UserId, actorId, action, details);

        await _db.SaveChangesAsync(cancellationToken);
        return MembershipResult.Ok(profile);
    }

    private bool HasFullAccess(MembershipProfile? profile, DateTime now)
    {
        if (profile is null)
        {
            return false;
        }

        if (profile.IsComplimentary)
        {
            return profile.ComplimentaryUntil is null ||
                   now <= AsUtc(profile.ComplimentaryUntil.Value);
        }

        var graceEnd = profile.CurrentPeriodEnd is null
            ? (DateTime?)null
            : AsUtc(profile.CurrentPeriodEnd.Value).AddDays(_options.GracePeriodDays);

        return profile.Status switch
        {
            MembershipStatus.Active or MembershipStatus.Trialing =>
                graceEnd is null || now <= graceEnd.Value,
            MembershipStatus.PastDue or MembershipStatus.Canceled =>
                graceEnd is not null && now <= graceEnd.Value,
            _ => false
        };
    }

    private static bool IsMember(MembershipProfile? profile, DateTime now)
    {
        if (profile is null)
        {
            return false;
        }

        if (profile.Status is not (MembershipStatus.Active or MembershipStatus.Trialing))
        {
            return false;
        }

        return profile.CurrentPeriodEnd is null ||
               now <= AsUtc(profile.CurrentPeriodEnd.Value);
    }

    private static bool IsComplimentary(MembershipProfile? profile, DateTime now) =>
        profile is { IsComplimentary: true } &&
        (profile.ComplimentaryUntil is null ||
         now <= AsUtc(profile.ComplimentaryUntil.Value));

    private async Task<MembershipProfile> GetOrCreateProfileAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _db.MembershipProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new MembershipProfile { UserId = userId };
            _db.MembershipProfiles.Add(profile);
        }

        return profile;
    }

    private void AddAuditLog(Guid targetUserId, Guid? actorId, string action, string? details)
    {
        _db.MembershipAuditLogs.Add(new MembershipAuditLog
        {
            TargetUserId = targetUserId,
            ActorUserId = actorId,
            Action = action,
            Details = details
        });
    }

    private static string? BuildDetails(string? note, DateTime? until)
    {
        if (string.IsNullOrWhiteSpace(note) && until is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (until is not null)
        {
            parts.Add($"bis {AsUtc(until.Value):yyyy-MM-dd HH:mm} UTC");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            parts.Add(note.Trim());
        }

        return string.Join(" · ", parts);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}