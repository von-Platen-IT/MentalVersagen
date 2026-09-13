using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Content;

/// <summary>Sort order for the public article overview (FeatureFix1 BR-082).</summary>
public enum ArticleSortOrder
{
    Newest,
    Oldest,
    MostLiked,
    Title
}

/// <summary>
/// Filter/query description for the public article list (FeatureFix1 BR-082).
/// </summary>
public sealed record ArticleQuery(
    int Page,
    int PageSize,
    ArticleCategory? Category = null,
    string? TagSlug = null,
    string? HashtagSlug = null,
    Guid? AuthorId = null,
    string? Search = null,
    ArticleAccessLevel? AccessLevel = null,
    ArticleSortOrder Sort = ArticleSortOrder.Newest);

/// <summary>
/// Application service for article read/write operations, slug uniqueness,
/// tag/hashtag management, ownership and the publish workflow.
/// </summary>
public interface IArticleService
{
    Task<(IReadOnlyList<Article> Items, int TotalCount)> SearchPublishedAsync(
        ArticleQuery query, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Article> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, ArticleCategory? category = null, string? tagSlug = null,
        CancellationToken cancellationToken = default);

    Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Article>> GetForAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>Articles the given author may manage (their own only).</summary>
    Task<IReadOnlyList<Article>> GetForAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hashtag>> GetAllHashtagsAsync(CancellationToken cancellationToken = default);

    /// <summary>Distinct authors that currently have published articles (for the author filter).</summary>
    Task<IReadOnlyList<User>> GetPublishedAuthorsAsync(CancellationToken cancellationToken = default);

    Task<string> EnsureUniqueSlugAsync(
        string baseSlug, Guid? excludeArticleId, CancellationToken cancellationToken = default);

    Task<Article> CreateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

    Task<Article> CreateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Publishes all scheduled articles whose time has come; returns the count.</summary>
    Task<int> PublishDueScheduledAsync(CancellationToken cancellationToken = default);

    Task SetVideoEmbedAsync(
        Guid articleId, string url, OEmbedResult resolved, CancellationToken cancellationToken = default);
}

public sealed class ArticleService : IArticleService
{
    private readonly AppDbContext _db;
    private readonly ISlugGenerator _slugGenerator;

    public ArticleService(AppDbContext db, ISlugGenerator slugGenerator)
    {
        _db = db;
        _slugGenerator = slugGenerator;
    }

    public async Task<(IReadOnlyList<Article> Items, int TotalCount)> SearchPublishedAsync(
        ArticleQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var articles = _db.Articles
            .AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published && a.PublishedAt != null);

        if (query.Category is not null)
        {
            articles = articles.Where(a => a.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.TagSlug))
        {
            articles = articles.Where(a => a.ArticleTags.Any(at => at.Tag!.Slug == query.TagSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.HashtagSlug))
        {
            articles = articles.Where(a => a.ArticleHashtags.Any(ah => ah.Hashtag!.Slug == query.HashtagSlug));
        }

        if (query.AuthorId is not null)
        {
            articles = articles.Where(a => a.AuthorId == query.AuthorId);
        }

        if (query.AccessLevel is not null)
        {
            articles = articles.Where(a => a.AccessLevel == query.AccessLevel);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            articles = articles.Where(a =>
                a.Title.ToLower().Contains(term) ||
                (a.Excerpt != null && a.Excerpt.ToLower().Contains(term)));
        }

        var total = await articles.CountAsync(cancellationToken);

        articles = query.Sort switch
        {
            ArticleSortOrder.Oldest => articles.OrderBy(a => a.PublishedAt),
            ArticleSortOrder.Title => articles.OrderBy(a => a.Title),
            ArticleSortOrder.MostLiked => articles
                .OrderByDescending(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbUp))
                .ThenByDescending(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbDown))
                .ThenByDescending(a => a.PublishedAt),
            _ => articles.OrderByDescending(a => a.PublishedAt)
        };

        var items = await articles
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.ArticleHashtags).ThenInclude(ah => ah.Hashtag)
            .Include(a => a.MediaAssets)
            .AsSplitQuery()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<(IReadOnlyList<Article> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, ArticleCategory? category = null, string? tagSlug = null,
        CancellationToken cancellationToken = default)
    {
        return SearchPublishedAsync(
            new ArticleQuery(page, pageSize, category, tagSlug), cancellationToken);
    }

    public Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.ArticleHashtags).ThenInclude(ah => ah.Hashtag)
            .Include(a => a.VideoEmbeds)
            .Include(a => a.MediaAssets)
            .FirstOrDefaultAsync(
                a => a.Slug == slug && a.Status == ArticleStatus.Published && a.PublishedAt != null,
                cancellationToken);
    }

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Articles
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.ArticleHashtags).ThenInclude(ah => ah.Hashtag)
            .Include(a => a.VideoEmbeds)
            .Include(a => a.MediaAssets)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Article>> GetForAdminAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Article>> GetForAuthorAsync(
        Guid authorId, CancellationToken cancellationToken = default)
    {
        return await _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Where(a => a.AuthorId == authorId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Hashtag>> GetAllHashtagsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Hashtags
            .AsNoTracking()
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetPublishedAuthorsAsync(
        CancellationToken cancellationToken = default)
    {
        var authors = await _db.Articles
            .AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published && a.PublishedAt != null && a.Author != null)
            .Select(a => a.Author!)
            .ToListAsync(cancellationToken);

        return authors
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    public async Task<string> EnsureUniqueSlugAsync(
        string baseSlug, Guid? excludeArticleId, CancellationToken cancellationToken = default)
    {
        var slug = _slugGenerator.Generate(baseSlug);
        if (string.IsNullOrEmpty(slug))
        {
            slug = "artikel";
        }

        var candidate = slug;
        var suffix = 2;

        while (await _db.Articles.AnyAsync(
                   a => a.Slug == candidate && (excludeArticleId == null || a.Id != excludeArticleId),
                   cancellationToken))
        {
            candidate = $"{slug}-{suffix++}";
        }

        return candidate;
    }

    public Task<Article> CreateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        return CreateAsync(article, tagNames, [], cancellationToken);
    }

    public async Task<Article> CreateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default)
    {
        article.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(article.Slug) ? article.Title : article.Slug, null, cancellationToken);

        article.UpdatedAt = DateTime.UtcNow;
        ApplyPublishTiming(article);

        _db.Articles.Add(article);
        await _db.SaveChangesAsync(cancellationToken);

        await SyncTagsAsync(article, tagNames, cancellationToken);
        await SyncHashtagsAsync(article, hashtagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return article;
    }

    public Task UpdateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(article, tagNames, [], cancellationToken);
    }

    public async Task UpdateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default)
    {
        article.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(article.Slug) ? article.Title : article.Slug, article.Id, cancellationToken);

        ApplyPublishTiming(article);

        article.UpdatedAt = DateTime.UtcNow;

        _db.Articles.Update(article);
        await _db.SaveChangesAsync(cancellationToken);

        await SyncTagsAsync(article, tagNames, cancellationToken);
        await SyncHashtagsAsync(article, hashtagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sets <c>PublishedAt</c> once on first publish and keeps it for scheduled
    /// publishing (FeatureFix1 BR-031/BR-032/BR-034).
    /// </summary>
    private static void ApplyPublishTiming(Article article)
    {
        if (article.Status == ArticleStatus.Published && article.PublishedAt is null)
        {
            article.PublishedAt = DateTime.UtcNow;
        }

        if (article.Status != ArticleStatus.Scheduled)
        {
            article.ScheduledAt = null;
        }
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return;
        }

        article.DeletedAt = DateTime.UtcNow;
        article.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PublishDueScheduledAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var due = await _db.Articles
            .Where(a => a.Status == ArticleStatus.Scheduled &&
                        a.ScheduledAt != null &&
                        a.ScheduledAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var article in due)
        {
            article.Status = ArticleStatus.Published;
            article.PublishedAt ??= article.ScheduledAt ?? now;
            article.ScheduledAt = null;
            article.UpdatedAt = now;
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    public async Task SetVideoEmbedAsync(
        Guid articleId, string url, OEmbedResult resolved, CancellationToken cancellationToken = default)
    {
        var existing = await _db.VideoEmbeds
            .Where(v => v.ArticleId == articleId)
            .ToListAsync(cancellationToken);

        _db.VideoEmbeds.RemoveRange(existing);

        if (!string.IsNullOrWhiteSpace(url))
        {
            _db.VideoEmbeds.Add(new VideoEmbed
            {
                OwnerType = MediaOwnerType.Article,
                ArticleId = articleId,
                Platform = resolved.Platform,
                OriginalUrl = url,
                EmbedHtml = resolved.EmbedHtml ?? string.Empty,
                ThumbnailUrl = resolved.ThumbnailUrl
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncTagsAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken)
    {
        var requested = Normalize(tagNames);

        var existingLinks = await _db.ArticleTags
            .Where(at => at.ArticleId == article.Id)
            .ToListAsync(cancellationToken);

        _db.ArticleTags.RemoveRange(existingLinks);

        foreach (var name in requested)
        {
            var slug = _slugGenerator.Generate(name);
            if (string.IsNullOrEmpty(slug))
            {
                continue;
            }

            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
                await _db.SaveChangesAsync(cancellationToken);
            }

            _db.ArticleTags.Add(new ArticleTag { ArticleId = article.Id, TagId = tag.Id });
        }
    }

    private async Task SyncHashtagsAsync(
        Article article, IEnumerable<string> hashtagNames, CancellationToken cancellationToken)
    {
        var requested = Normalize(hashtagNames.Select(n => n.TrimStart('#')));

        var existingLinks = await _db.ArticleHashtags
            .Where(ah => ah.ArticleId == article.Id)
            .ToListAsync(cancellationToken);

        _db.ArticleHashtags.RemoveRange(existingLinks);

        foreach (var name in requested)
        {
            var slug = _slugGenerator.Generate(name);
            if (string.IsNullOrEmpty(slug))
            {
                continue;
            }

            var hashtag = await _db.Hashtags.FirstOrDefaultAsync(h => h.Slug == slug, cancellationToken);
            if (hashtag is null)
            {
                hashtag = new Hashtag { Name = name, Slug = slug };
                _db.Hashtags.Add(hashtag);
                await _db.SaveChangesAsync(cancellationToken);
            }

            _db.ArticleHashtags.Add(new ArticleHashtag { ArticleId = article.Id, HashtagId = hashtag.Id });
        }
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
