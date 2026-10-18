using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Activity;

/// <summary>
/// Append-only activity log backed by a single <see cref="ActivityLogEntry"/> table.
/// </summary>
public sealed class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _db;

    public ActivityLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogArticleViewAsync(
        Guid articleId, Guid? userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        // Denormalisierter Zähler für Sortierung und Badges der Landing Page
        // (Article.ViewCount). Der Zähler wird im selben SaveChanges wie das
        // Event gespeichert — eine Transaktion, kein Auseinanderlaufen.
        var article = await _db.Articles.FindAsync(new object[] { articleId }, cancellationToken);
        if (article is not null)
        {
            article.ViewCount++;
        }

        await AddAsync(ActivityEventType.ArticleViewed, articleId, null, null, userId, ipAddress, cancellationToken);
    }

    public Task LogArticleCreatedAsync(
        Guid articleId, Guid? userId, string? ipAddress, CancellationToken cancellationToken = default)
        => AddAsync(ActivityEventType.ArticleCreated, articleId, null, null, userId, ipAddress, cancellationToken);

    public Task LogCommentCreatedAsync(
        Guid articleId, Guid commentId, Guid? userId, string? ipAddress,
        CancellationToken cancellationToken = default)
        => AddAsync(ActivityEventType.CommentCreated, articleId, commentId, null, userId, ipAddress, cancellationToken);

    public Task LogArticleRatedAsync(
        Guid articleId, RatingValue value, Guid? userId, string? ipAddress,
        CancellationToken cancellationToken = default)
        => AddAsync(ActivityEventType.ArticleRated, articleId, null, value, userId, ipAddress, cancellationToken);

    private async Task AddAsync(
        ActivityEventType eventType, Guid? articleId, Guid? commentId, RatingValue? ratingValue,
        Guid? userId, string? ipAddress, CancellationToken cancellationToken)
    {
        _db.ActivityLogEntries.Add(new ActivityLogEntry
        {
            EventType = eventType,
            ArticleId = articleId,
            CommentId = commentId,
            RatingValue = ratingValue,
            UserId = userId,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActivityOverview> GetOverviewAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // Pull the minimal projection for the range and aggregate in memory; the
        // volume is small (a side feature) and this keeps the query provider-agnostic.
        var entries = await _db.ActivityLogEntries
            .AsNoTracking()
            .Where(e => e.CreatedAt >= fromUtc && e.CreatedAt < toUtc)
            .Select(e => new { e.EventType, e.ArticleId, e.RatingValue, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var totalViews = entries.Count(e => e.EventType == ActivityEventType.ArticleViewed);
        var totalArticles = entries.Count(e => e.EventType == ActivityEventType.ArticleCreated);
        var totalComments = entries.Count(e => e.EventType == ActivityEventType.CommentCreated);
        var totalThumbUp = entries.Count(e =>
            e.EventType == ActivityEventType.ArticleRated && e.RatingValue == RatingValue.ThumbUp);
        var totalThumbDown = entries.Count(e =>
            e.EventType == ActivityEventType.ArticleRated && e.RatingValue == RatingValue.ThumbDown);

        var viewCounts = entries
            .Where(e => e.EventType == ActivityEventType.ArticleViewed && e.ArticleId is not null)
            .GroupBy(e => e.ArticleId!.Value)
            .Select(g => new { ArticleId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var ratingCounts = entries
            .Where(e => e.EventType == ActivityEventType.ArticleRated && e.ArticleId is not null)
            .GroupBy(e => e.ArticleId!.Value)
            .Select(g => new
            {
                ArticleId = g.Key,
                Up = g.Count(x => x.RatingValue == RatingValue.ThumbUp),
                Down = g.Count(x => x.RatingValue == RatingValue.ThumbDown)
            })
            .OrderByDescending(x => x.Up)
            .Take(10)
            .ToList();

        var articleIds = viewCounts.Select(x => x.ArticleId)
            .Concat(ratingCounts.Select(x => x.ArticleId))
            .Distinct()
            .ToList();

        var titles = await _db.Articles
            .AsNoTracking()
            .Where(a => articleIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title })
            .ToDictionaryAsync(a => a.Id, a => a.Title, cancellationToken);

        string TitleOf(Guid id) => titles.TryGetValue(id, out var title) ? title : "(gelöscht)";

        var topViewed = viewCounts
            .Select(x => new ArticleActivity(x.ArticleId, TitleOf(x.ArticleId), x.Count))
            .ToList();

        var articleRatings = ratingCounts
            .Select(x => new ArticleRatingActivity(x.ArticleId, TitleOf(x.ArticleId), x.Up, x.Down))
            .ToList();

        var daily = entries
            .GroupBy(e => e.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyActivity(
                g.Key,
                g.Count(x => x.EventType == ActivityEventType.ArticleViewed),
                g.Count(x => x.EventType == ActivityEventType.CommentCreated),
                g.Count(x => x.EventType == ActivityEventType.ArticleCreated)))
            .ToList();

        var recent = await _db.ActivityLogEntries
            .AsNoTracking()
            .Where(e => e.CreatedAt >= fromUtc && e.CreatedAt < toUtc)
            .Include(e => e.Article)
            .Include(e => e.User)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new ActivityOverview(
            totalViews, totalArticles, totalComments, totalThumbUp, totalThumbDown,
            topViewed, articleRatings, daily, recent);
    }

    public async Task<IReadOnlyList<ArticleViewHistory>> GetArticleViewHistoryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // Same aggregation style as GetOverviewAsync: pull the minimal rows and
        // aggregate in memory — the volume is small and provider-agnostic.
        var rows = await _db.ActivityLogEntries
            .AsNoTracking()
            .Where(e => e.EventType == ActivityEventType.ArticleViewed
                        && e.ArticleId != null
                        && e.CreatedAt >= fromUtc && e.CreatedAt < toUtc)
            .Select(e => new { e.ArticleId, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.ArticleId!.Value).Distinct().ToList();
        var articles = await _db.Articles
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Title, a.ViewCount })
            .ToListAsync(cancellationToken);

        return articles
            .Select(a =>
            {
                var mine = rows.Where(r => r.ArticleId == a.Id).ToList();
                return new ArticleViewHistory(
                    a.Id,
                    a.Title,
                    a.ViewCount,
                    mine.Count,
                    mine.Count == 0 ? null : mine.Max(r => r.CreatedAt),
                    mine.GroupBy(r => r.CreatedAt.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new DailyViewCount(g.Key, g.Count()))
                        .ToList());
            })
            .OrderByDescending(h => h.PeriodViews)
            .ThenByDescending(h => h.OverallViews)
            .ToList();
    }
}
