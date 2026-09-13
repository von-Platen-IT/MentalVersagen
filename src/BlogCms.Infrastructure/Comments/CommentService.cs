using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Comments;

public sealed record CommentResult(bool Succeeded, string? Error, Comment? Comment = null)
{
    public static CommentResult Ok(Comment comment) => new(true, null, comment);

    public static CommentResult Fail(string error) => new(false, error);
}

public sealed record ReportGroup(Comment Comment, int ReportCount);

public interface ICommentService
{
    Task<IReadOnlyList<Comment>> GetThreadAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<CommentResult> AddAsync(
        Guid articleId, Guid userId, string content, Guid? parentCommentId,
        CancellationToken cancellationToken = default);

    Task<CommentResult> EditAsync(
        Guid commentId, Guid userId, string content, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(Guid commentId, Guid userId, bool isModerator, CancellationToken cancellationToken = default);

    Task<bool> ReportAsync(
        Guid commentId, Guid userId, ReportReason reason, string? note, CancellationToken cancellationToken = default);

    Task<CommentResult> ModerateAsync(
        Guid commentId, bool approve, CancellationToken cancellationToken = default);

    /// <summary>
    /// Highlights/unhighlights a comment (FeatureFix1 BR-063). Allowed for admins and
    /// for the author of the comment's article; enforced server-side.
    /// </summary>
    Task<CommentResult> SetHighlightAsync(
        Guid commentId, bool highlighted, Guid actorUserId, bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportGroup>> GetOpenReportsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetPendingAndFlaggedAsync(CancellationToken cancellationToken = default);

    Task DismissReportsForCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles comment creation (with moderation mode, blocklist filter and rate
/// limiting), editing, soft deletion and reporting.
/// </summary>
public sealed class CommentService : ICommentService
{
    private readonly AppDbContext _db;
    private readonly CommentOptions _options;

    public CommentService(AppDbContext db, IOptions<CommentOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Comment>> GetThreadAsync(
        Guid articleId, CancellationToken cancellationToken = default)
    {
        // Approved comments plus soft-deleted ones (shown as placeholders so the
        // thread structure is preserved). Pending/flagged/rejected are hidden.
        var comments = await _db.Comments
            .Include(c => c.User)
            .Where(c => c.ArticleId == articleId &&
                        (c.Status == CommentStatus.Approved || c.DeletedAt != null))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        // Attach comment media/video via relationship fixup (tracked queries), which
        // avoids DB-side collection includes and keeps the query provider-agnostic.
        if (comments.Count > 0)
        {
            var commentIds = comments.Select(c => c.Id).ToList();

            await _db.MediaAssets
                .Where(m => m.CommentId != null && commentIds.Contains(m.CommentId.Value))
                .ToListAsync(cancellationToken);

            await _db.VideoEmbeds
                .Where(v => v.CommentId != null && commentIds.Contains(v.CommentId.Value))
                .ToListAsync(cancellationToken);
        }

        return comments;
    }

    public async Task<CommentResult> AddAsync(
        Guid articleId, Guid userId, string content, Guid? parentCommentId,
        CancellationToken cancellationToken = default)
    {
        content = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return CommentResult.Fail("Kommentar darf nicht leer sein.");
        }

        if (content.Length > 5000)
        {
            return CommentResult.Fail("Kommentar ist zu lang (max. 5000 Zeichen).");
        }

        // Rate limiting per user.
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recentCount = await _db.Comments
            .CountAsync(c => c.UserId == userId && c.CreatedAt >= oneMinuteAgo, cancellationToken);

        if (recentCount >= _options.MaxCommentsPerMinute)
        {
            return CommentResult.Fail(
                $"Zu viele Kommentare. Bitte warte einen Moment (max. {_options.MaxCommentsPerMinute} pro Minute).");
        }

        // Replies must reference a comment on the same article.
        if (parentCommentId is not null)
        {
            var parentValid = await _db.Comments.AnyAsync(
                c => c.Id == parentCommentId && c.ArticleId == articleId, cancellationToken);
            if (!parentValid)
            {
                return CommentResult.Fail("Der Kommentar, auf den du antwortest, existiert nicht.");
            }
        }

        var status = DetermineInitialStatus(content);

        var comment = new Comment
        {
            ArticleId = articleId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            ContentText = content,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        comment.User = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return CommentResult.Ok(comment);
    }

    private CommentStatus DetermineInitialStatus(string content)
    {
        // Automated blocklist filter always wins, regardless of moderation mode.
        foreach (var term in _options.Blocklist)
        {
            if (!string.IsNullOrWhiteSpace(term) &&
                content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return CommentStatus.Flagged;
            }
        }

        return _options.ModerationMode == CommentModerationMode.PreModeration
            ? CommentStatus.Pending
            : CommentStatus.Approved;
    }

    public async Task<CommentResult> EditAsync(
        Guid commentId, Guid userId, string content, CancellationToken cancellationToken = default)
    {
        content = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return CommentResult.Fail("Kommentar darf nicht leer sein.");
        }

        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return CommentResult.Fail("Kommentar nicht gefunden.");
        }

        if (comment.UserId != userId)
        {
            return CommentResult.Fail("Du kannst nur eigene Kommentare bearbeiten.");
        }

        if (comment.DeletedAt is not null)
        {
            return CommentResult.Fail("Gelöschte Kommentare können nicht bearbeitet werden.");
        }

        var editableUntil = comment.CreatedAt.AddMinutes(_options.EditWindowMinutes);
        if (DateTime.UtcNow > editableUntil)
        {
            return CommentResult.Fail(
                $"Das Bearbeitungsfenster von {_options.EditWindowMinutes} Minuten ist abgelaufen.");
        }

        comment.ContentText = content;
        comment.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return CommentResult.Ok(comment);
    }

    public async Task<bool> SoftDeleteAsync(
        Guid commentId, Guid userId, bool isModerator, CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null || comment.DeletedAt is not null)
        {
            return false;
        }

        if (comment.UserId != userId && !isModerator)
        {
            return false;
        }

        // Soft delete: keep the row so replies stay reachable; the UI shows a placeholder.
        comment.DeletedAt = DateTime.UtcNow;
        comment.ContentText = string.Empty;
        comment.Status = CommentStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ReportAsync(
        Guid commentId, Guid userId, ReportReason reason, string? note,
        CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return false;
        }

        var alreadyReported = await _db.Reports.AnyAsync(
            r => r.CommentId == commentId && r.ReportedByUserId == userId && r.Status == ReportStatus.Open,
            cancellationToken);

        if (alreadyReported)
        {
            return false;
        }

        _db.Reports.Add(new Report
        {
            CommentId = commentId,
            ReportedByUserId = userId,
            Reason = reason,
            Note = note,
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CommentResult> ModerateAsync(
        Guid commentId, bool approve, CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return CommentResult.Fail("Kommentar nicht gefunden.");
        }

        if (approve)
        {
            comment.Status = CommentStatus.Approved;
            comment.DeletedAt = null;
        }
        else
        {
            comment.Status = CommentStatus.Rejected;
            comment.DeletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CommentResult.Ok(comment);
    }

    public async Task<CommentResult> SetHighlightAsync(
        Guid commentId, bool highlighted, Guid actorUserId, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments
            .Include(c => c.Article)
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        if (comment is null)
        {
            return CommentResult.Fail("Kommentar nicht gefunden.");
        }

        var isArticleAuthor = comment.Article?.AuthorId == actorUserId;
        if (!isAdmin && !isArticleAuthor)
        {
            return CommentResult.Fail("Nur Admins oder der Autor des Beitrags dürfen Kommentare auszeichnen.");
        }

        comment.IsHighlighted = highlighted;
        await _db.SaveChangesAsync(cancellationToken);
        return CommentResult.Ok(comment);
    }

    public async Task<IReadOnlyList<ReportGroup>> GetOpenReportsAsync(
        CancellationToken cancellationToken = default)
    {
        var openReports = await _db.Reports
            .AsNoTracking()
            .Where(r => r.Status == ReportStatus.Open)
            .ToListAsync(cancellationToken);

        var grouped = openReports
            .GroupBy(r => r.CommentId)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var result = new List<ReportGroup>();
        foreach (var group in grouped)
        {
            var comment = await _db.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == group.CommentId, cancellationToken);

            if (comment is not null)
            {
                result.Add(new ReportGroup(comment, group.Count));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<Comment>> GetPendingAndFlaggedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Comments
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Article)
            .Where(c => c.Status == CommentStatus.Pending || c.Status == CommentStatus.Flagged)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task DismissReportsForCommentAsync(
        Guid commentId, CancellationToken cancellationToken = default)
    {
        var reports = await _db.Reports
            .Where(r => r.CommentId == commentId && r.Status == ReportStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var report in reports)
        {
            report.Status = ReportStatus.Dismissed;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
