using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;

namespace BlogCms.Infrastructure.Activity;

/// <summary>An article together with an aggregated event count.</summary>
public sealed record ArticleActivity(Guid ArticleId, string Title, int Count);

/// <summary>Aggregated thumbs up/down for a single article.</summary>
public sealed record ArticleRatingActivity(Guid ArticleId, string Title, int ThumbUp, int ThumbDown);

/// <summary>Per-day activity counts for the admin evaluation.</summary>
public sealed record DailyActivity(DateTime Date, int Views, int Comments, int ArticlesCreated);

/// <summary>Views of a single article on a single day.</summary>
public sealed record DailyViewCount(DateTime Date, int Views);

/// <summary>
/// View history of a single article for the admin evaluation: overall counter,
/// period count, last view and the per-day counts.
/// </summary>
public sealed record ArticleViewHistory(
    Guid ArticleId,
    string Title,
    int OverallViews,
    int PeriodViews,
    DateTime? LastViewedAt,
    IReadOnlyList<DailyViewCount> Daily);

/// <summary>Aggregated activity data for the admin evaluation page.</summary>
public sealed record ActivityOverview(
    int TotalViews,
    int TotalArticlesCreated,
    int TotalComments,
    int TotalThumbUp,
    int TotalThumbDown,
    IReadOnlyList<ArticleActivity> TopViewedArticles,
    IReadOnlyList<ArticleRatingActivity> ArticleRatings,
    IReadOnlyList<DailyActivity> Daily,
    IReadOnlyList<ActivityLogEntry> RecentEntries);

/// <summary>
/// Unified activity log: records article views, article creation, comment
/// creation and article ratings in a single table, and provides the
/// aggregated data for the admin evaluation page.
/// </summary>
public interface IActivityLogService
{
    Task LogArticleViewAsync(
        Guid articleId, Guid? userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task LogArticleCreatedAsync(
        Guid articleId, Guid? userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task LogCommentCreatedAsync(
        Guid articleId, Guid commentId, Guid? userId, string? ipAddress,
        CancellationToken cancellationToken = default);

    Task LogArticleRatedAsync(
        Guid articleId, RatingValue value, Guid? userId, string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated overview for the half-open interval [<paramref name="fromUtc"/>,
    /// <paramref name="toUtc"/>).
    /// </summary>
    Task<ActivityOverview> GetOverviewAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-article view history for the half-open interval
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>): overall counter,
    /// period count, last view and per-day counts ("how often and when was an
    /// article page opened").
    /// </summary>
    Task<IReadOnlyList<ArticleViewHistory>> GetArticleViewHistoryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
