using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Ratings;

/// <summary>Aggregated rating counts for a target.</summary>
public sealed record RatingSummary(int ThumbUp, int ThumbDown)
{
    /// <summary>Net score: thumbs up minus thumbs down.</summary>
    public int Score => ThumbUp - ThumbDown;

    public static readonly RatingSummary Empty = new(0, 0);
}

public sealed record RatingResult(bool Succeeded, string? Error, Rating? Rating = null)
{
    public static RatingResult Ok(Rating rating) => new(true, null, rating);

    public static RatingResult Fail(string error) => new(false, error);
}

/// <summary>
/// 👍/👎 ratings for articles and comments (FeatureFix1 BR-070/BR-071/BR-072).
/// A user holds at most one rating per target; re-rating updates the existing row.
/// </summary>
public interface IRatingService
{
    Task<RatingResult> RateArticleAsync(
        Guid articleId, Guid userId, RatingValue value, CancellationToken cancellationToken = default);

    Task<RatingResult> RateCommentAsync(
        Guid commentId, Guid userId, RatingValue value, CancellationToken cancellationToken = default);

    Task<RatingSummary> GetArticleSummaryAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, RatingSummary>> GetCommentSummariesAsync(
        IEnumerable<Guid> commentIds, CancellationToken cancellationToken = default);

    Task<RatingValue?> GetUserArticleRatingAsync(
        Guid articleId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, RatingValue>> GetUserCommentRatingsAsync(
        IEnumerable<Guid> commentIds, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class RatingService : IRatingService
{
    private readonly AppDbContext _db;

    public RatingService(AppDbContext db)
    {
        _db = db;
    }

    public Task<RatingResult> RateArticleAsync(
        Guid articleId, Guid userId, RatingValue value, CancellationToken cancellationToken = default)
    {
        return RateAsync(articleId: articleId, commentId: null, userId, value, cancellationToken);
    }

    public Task<RatingResult> RateCommentAsync(
        Guid commentId, Guid userId, RatingValue value, CancellationToken cancellationToken = default)
    {
        return RateAsync(articleId: null, commentId: commentId, userId, value, cancellationToken);
    }

    private async Task<RatingResult> RateAsync(
        Guid? articleId, Guid? commentId, Guid userId, RatingValue value,
        CancellationToken cancellationToken)
    {
        // Anonymous ratings are impossible (no user id) — guarded by the caller/UI as well.
        if (userId == Guid.Empty)
        {
            return RatingResult.Fail("Zum Bewerten ist eine Anmeldung erforderlich.");
        }

        var exists = articleId is not null
            ? await _db.Articles.AnyAsync(a => a.Id == articleId, cancellationToken)
            : await _db.Comments.AnyAsync(c => c.Id == commentId, cancellationToken);

        if (!exists)
        {
            return RatingResult.Fail("Das zu bewertende Objekt existiert nicht.");
        }

        var rating = articleId is not null
            ? await _db.Ratings.FirstOrDefaultAsync(
                r => r.UserId == userId && r.ArticleId == articleId, cancellationToken)
            : await _db.Ratings.FirstOrDefaultAsync(
                r => r.UserId == userId && r.CommentId == commentId, cancellationToken);

        if (rating is null)
        {
            rating = new Rating
            {
                UserId = userId,
                ArticleId = articleId,
                CommentId = commentId,
                Value = value,
                CreatedAt = DateTime.UtcNow
            };
            _db.Ratings.Add(rating);
        }
        else
        {
            rating.Value = value;
            rating.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return RatingResult.Ok(rating);
    }

    public async Task<RatingSummary> GetArticleSummaryAsync(
        Guid articleId, CancellationToken cancellationToken = default)
    {
        var ratings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.ArticleId == articleId)
            .Select(r => r.Value)
            .ToListAsync(cancellationToken);

        return Summarize(ratings);
    }

    public async Task<IReadOnlyDictionary<Guid, RatingSummary>> GetCommentSummariesAsync(
        IEnumerable<Guid> commentIds, CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RatingSummary>();
        }

        var ratings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.CommentId != null && ids.Contains(r.CommentId.Value))
            .Select(r => new { CommentId = r.CommentId!.Value, r.Value })
            .ToListAsync(cancellationToken);

        return ratings
            .GroupBy(r => r.CommentId)
            .ToDictionary(g => g.Key, g => Summarize(g.Select(x => x.Value)));
    }

    public async Task<RatingValue?> GetUserArticleRatingAsync(
        Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        return await _db.Ratings
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ArticleId == articleId)
            .Select(r => (RatingValue?)r.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, RatingValue>> GetUserCommentRatingsAsync(
        IEnumerable<Guid> commentIds, Guid userId, CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (userId == Guid.Empty || ids.Count == 0)
        {
            return new Dictionary<Guid, RatingValue>();
        }

        var ratings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.CommentId != null && ids.Contains(r.CommentId.Value))
            .Select(r => new { CommentId = r.CommentId!.Value, r.Value })
            .ToListAsync(cancellationToken);

        return ratings.ToDictionary(r => r.CommentId, r => r.Value);
    }

    private static RatingSummary Summarize(IEnumerable<RatingValue> values)
    {
        var up = 0;
        var down = 0;
        foreach (var value in values)
        {
            if (value == RatingValue.ThumbUp)
            {
                up++;
            }
            else
            {
                down++;
            }
        }

        return new RatingSummary(up, down);
    }
}
