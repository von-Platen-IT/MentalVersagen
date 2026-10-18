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
    Title,

    /// <summary>Most interactions overall (ratings + comments).</summary>
    Popular,

    /// <summary>Most comments first.</summary>
    MostCommented,

    /// <summary>Most thumbs-up first.</summary>
    MostUpvoted,

    /// <summary>Most thumbs-down first.</summary>
    MostDownvoted,

    /// <summary>Most page views first (Article.ViewCount).</summary>
    MostViewed,

    /// <summary>Fewest page views first (Article.ViewCount).</summary>
    LeastViewed,

    /// <summary>Fewest thumbs-up first.</summary>
    LeastUpvoted,

    /// <summary>Fewest thumbs-down first.</summary>
    LeastDownvoted
}

/// <summary>A hashtag together with the number of published articles using it.</summary>
public sealed record HashtagUsage(string Name, string Slug, int Count);

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
    ArticleSortOrder Sort = ArticleSortOrder.Newest,
    DateTime? DateFrom = null,
    DateTime? DateTo = null);

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

    /// <summary>
    /// Loads any non-deleted article by slug regardless of publication status
    /// (used for author/admin preview of drafts, scheduled and archived articles).
    /// </summary>
    Task<Article?> GetBySlugForPreviewAsync(string slug, CancellationToken cancellationToken = default);

    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Article>> GetForAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>Articles the given author may manage (their own only).</summary>
    Task<IReadOnlyList<Article>> GetForAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side filtered and searched list of articles for the admin/author
    /// management table (/Admin/Articles).
    /// </summary>
    Task<IReadOnlyList<Article>> GetForManagementAsync(
        Guid? authorId, ArticleStatus? statusFilter = null, string? search = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hashtag>> GetAllHashtagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hashtags used by at least one published article, with usage counts
    /// (for the autocomplete REST endpoint).
    /// </summary>
    Task<IReadOnlyList<HashtagUsage>> GetPublishedHashtagsAsync(CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Prepares a new article for saving (slug, publish timing, tag/hashtag sync)
    /// WITHOUT calling SaveChanges — used by <see cref="ArticlePostingService"/>
    /// inside its transaction. The public <see cref="CreateAsync"/> wraps this with
    /// a single SaveChanges.
    /// </summary>
    Task PrepareCreateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares an existing article for saving. The slug is only regenerated when it
    /// actually changed, so published URLs stay stable across title edits. No
    /// SaveChanges — used by <see cref="ArticlePostingService"/> inside its
    /// transaction.
    /// </summary>
    Task PrepareUpdateAsync(
        Article article, string? originalSlug, IEnumerable<string> tagNames,
        IEnumerable<string> hashtagNames, CancellationToken cancellationToken = default);

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
            // Normalize the incoming slug through the same SlugGenerator used at
            // write time so URL params match regardless of casing, umlauts ("WÄHLEN")
            // or a leading '#' ("#waehlen"). The .ToLower() on the column side
            // additionally tolerates historical rows with mixed-case slugs.
            var tagSlug = _slugGenerator.Generate(query.TagSlug);
            if (!string.IsNullOrEmpty(tagSlug))
            {
                articles = articles.Where(a => a.ArticleTags.Any(at => at.Tag!.Slug.ToLower() == tagSlug));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.HashtagSlug))
        {
            var hashtagSlug = _slugGenerator.Generate(query.HashtagSlug);
            if (!string.IsNullOrEmpty(hashtagSlug))
            {
                articles = articles.Where(a => a.ArticleHashtags.Any(ah => ah.Hashtag!.Slug.ToLower() == hashtagSlug));
            }
        }

        if (query.AuthorId is not null)
        {
            articles = articles.Where(a => a.AuthorId == query.AuthorId);
        }

        if (query.AccessLevel is not null)
        {
            articles = articles.Where(a => a.AccessLevel == query.AccessLevel);
        }

        if (query.DateFrom is not null)
        {
            articles = articles.Where(a => a.PublishedAt >= query.DateFrom);
        }

        if (query.DateTo is not null)
        {
            // Inklusives Tagesende: "bis 24.09." umfasst den gesamten Tag.
            var toExclusive = query.DateTo.Value.Date.AddDays(1);
            articles = articles.Where(a => a.PublishedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Volltextsuche über Titel, Kurzbeschreibung und Inhalt.
            var term = query.Search.Trim().ToLowerInvariant();
            articles = articles.Where(a =>
                a.Title.ToLower().Contains(term) ||
                (a.Excerpt != null && a.Excerpt.ToLower().Contains(term)) ||
                a.ContentMarkdown.ToLower().Contains(term));
        }

        var total = await articles.CountAsync(cancellationToken);

        articles = query.Sort switch
        {
            ArticleSortOrder.Oldest => articles
                .OrderBy(a => a.PublishedAt)
                .ThenBy(a => a.CreatedAt)
                .ThenBy(a => a.Id),
            ArticleSortOrder.Title => articles
                .OrderBy(a => a.Title)
                .ThenByDescending(a => a.PublishedAt),
            ArticleSortOrder.MostLiked => articles
                .OrderByDescending(a =>
                    a.Ratings.Count(r => r.Value == RatingValue.ThumbUp) -
                    a.Ratings.Count(r => r.Value == RatingValue.ThumbDown))
                .ThenByDescending(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbUp))
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.MostUpvoted => articles
                .OrderByDescending(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbUp))
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.MostDownvoted => articles
                .OrderByDescending(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbDown))
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.MostViewed => articles
                .OrderByDescending(a => a.ViewCount)
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.LeastViewed => articles
                .OrderBy(a => a.ViewCount)
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.LeastUpvoted => articles
                .OrderBy(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbUp))
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.LeastDownvoted => articles
                .OrderBy(a => a.Ratings.Count(r => r.Value == RatingValue.ThumbDown))
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.MostCommented => articles
                .OrderByDescending(a => a.Comments.Count)
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            ArticleSortOrder.Popular => articles
                .OrderByDescending(a => a.Ratings.Count + a.Comments.Count)
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt),
            _ => articles
                .OrderByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
        };

        var items = await articles
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.ArticleHashtags).ThenInclude(ah => ah.Hashtag)
            .Include(a => a.MediaAssets)
            .Include(a => a.Ratings)
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

    public Task<Article?> GetBySlugForPreviewAsync(string slug, CancellationToken cancellationToken = default)
    {
        return _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.ArticleHashtags).ThenInclude(ah => ah.Hashtag)
            .Include(a => a.VideoEmbeds)
            .Include(a => a.MediaAssets)
            .FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
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

    public Task<IReadOnlyList<Article>> GetForAdminAsync(CancellationToken cancellationToken = default)
        => GetForManagementAsync(authorId: null, cancellationToken: cancellationToken);

    public Task<IReadOnlyList<Article>> GetForAuthorAsync(
        Guid authorId, CancellationToken cancellationToken = default)
        => GetForManagementAsync(authorId: authorId, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<Article>> GetForManagementAsync(
        Guid? authorId, ArticleStatus? statusFilter = null, string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Include(a => a.MediaAssets)
            .AsQueryable();

        if (authorId is not null)
        {
            query = query.Where(a => a.AuthorId == authorId);
        }

        if (statusFilter is not null)
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Slug.ToLower().Contains(term) ||
                (a.Excerpt != null && a.Excerpt.ToLower().Contains(term)));
        }

        return await query
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

    public async Task<IReadOnlyList<HashtagUsage>> GetPublishedHashtagsAsync(
        CancellationToken cancellationToken = default)
    {
        // Group by normalized slug so transliteration variants (e.g. "Wählen" vs.
        // "waehlen") collapse into a single entry instead of appearing twice.
        var rows = await _db.ArticleHashtags
            .AsNoTracking()
            .Where(ah => ah.Article!.Status == ArticleStatus.Published && ah.Article.PublishedAt != null)
            .GroupBy(ah => ah.Hashtag!.Slug.ToLower())
            .Select(g => new
            {
                Slug = g.Key,
                Count = g.Count(),
                Name = g.Min(ah => ah.Hashtag!.Name)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new HashtagUsage(r.Name ?? string.Empty, r.Slug, r.Count)).ToList();
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
        await PrepareCreateAsync(article, tagNames, hashtagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task PrepareCreateAsync(
        Article article, IEnumerable<string> tagNames, IEnumerable<string> hashtagNames,
        CancellationToken cancellationToken = default)
    {
        article.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(article.Slug) ? article.Title : article.Slug, null, cancellationToken);

        article.UpdatedAt = DateTime.UtcNow;
        ApplyPublishTiming(article);

        _db.Articles.Add(article);
        await SyncTagsAsync(article, tagNames, cancellationToken);
        await SyncHashtagsAsync(article, hashtagNames, cancellationToken);
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
        await PrepareUpdateAsync(article, article.Slug, tagNames, hashtagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task PrepareUpdateAsync(
        Article article, string? originalSlug, IEnumerable<string> tagNames,
        IEnumerable<string> hashtagNames, CancellationToken cancellationToken = default)
    {
        await ApplyStableSlugAsync(article, originalSlug, cancellationToken);
        ApplyPublishTiming(article);
        article.UpdatedAt = DateTime.UtcNow;

        _db.Articles.Update(article);
        await SyncTagsAsync(article, tagNames, cancellationToken);
        await SyncHashtagsAsync(article, hashtagNames, cancellationToken);
    }

    /// <summary>
    /// URL-Stabilität (docs/02-admin-artikelverwaltung.md): Ein leerer oder
    /// unveränderter Slug lässt den bisherigen Slug unverändert; nur eine echte
    /// Änderung wird auf Eindeutigkeit geprüft.
    /// </summary>
    private async Task ApplyStableSlugAsync(
        Article article, string? originalSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(article.Slug))
        {
            article.Slug = string.IsNullOrWhiteSpace(originalSlug)
                ? await EnsureUniqueSlugAsync(article.Title, article.Id, cancellationToken)
                : originalSlug;
            return;
        }

        if (!string.Equals(article.Slug, originalSlug, StringComparison.Ordinal))
        {
            article.Slug = await EnsureUniqueSlugAsync(article.Slug, article.Id, cancellationToken);
        }
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
        var requested = NormalizeBySlug(tagNames, stripHash: false);

        var existingLinks = await _db.ArticleTags
            .Where(at => at.ArticleId == article.Id)
            .ToListAsync(cancellationToken);

        _db.ArticleTags.RemoveRange(existingLinks);

        // Track resolved tags per slug within this batch so transliteration
        // variants collapse onto one row even before SaveChanges materialises
        // the just-added entity (EF's query pipeline won't see Added entries).
        var resolvedBySlug = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, slug) in requested)
        {
            if (resolvedBySlug.TryGetValue(slug, out var tag))
            {
                _db.ArticleTags.Add(new ArticleTag { ArticleId = article.Id, TagId = tag.Id });
                continue;
            }

            tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug.ToLower() == slug, cancellationToken);
            if (tag is null)
            {
                // GUID primary keys are assigned client-side on Add, so the join
                // entry can reference the new tag without an intermediate SaveChanges.
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
            }

            resolvedBySlug[slug] = tag;
            _db.ArticleTags.Add(new ArticleTag { ArticleId = article.Id, TagId = tag.Id });
        }
    }

    private async Task SyncHashtagsAsync(
        Article article, IEnumerable<string> hashtagNames, CancellationToken cancellationToken)
    {
        var requested = NormalizeBySlug(hashtagNames, stripHash: true);

        var existingLinks = await _db.ArticleHashtags
            .Where(ah => ah.ArticleId == article.Id)
            .ToListAsync(cancellationToken);

        _db.ArticleHashtags.RemoveRange(existingLinks);

        // See SyncTagsAsync for why the in-batch slug map is needed.
        var resolvedBySlug = new Dictionary<string, Hashtag>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, slug) in requested)
        {
            if (resolvedBySlug.TryGetValue(slug, out var hashtag))
            {
                _db.ArticleHashtags.Add(new ArticleHashtag { ArticleId = article.Id, HashtagId = hashtag.Id });
                continue;
            }

            hashtag = await _db.Hashtags.FirstOrDefaultAsync(h => h.Slug.ToLower() == slug, cancellationToken);
            if (hashtag is null)
            {
                hashtag = new Hashtag { Name = name, Slug = slug };
                _db.Hashtags.Add(hashtag);
            }

            resolvedBySlug[slug] = hashtag;
            _db.ArticleHashtags.Add(new ArticleHashtag { ArticleId = article.Id, HashtagId = hashtag.Id });
        }
    }

    /// <summary>
    /// Cleans raw user input into unique (Name, Slug) pairs. Uniqueness is decided
    /// by the <em>slug</em> (not the display name), so transliteration variants
    /// such as "Wählen" / "waehlen" / "WAEHLEN" collapse onto one entry. The
    /// first spelling encountered wins as the canonical display name.
    /// </summary>
    private List<(string Name, string Slug)> NormalizeBySlug(
        IEnumerable<string> values, bool stripHash)
    {
        var result = new List<(string, string)>();
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in values)
        {
            var cleaned = stripHash ? raw.TrimStart('#').Trim() : raw.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var slug = _slugGenerator.Generate(cleaned);
            if (string.IsNullOrEmpty(slug))
            {
                continue;
            }

            if (!seenSlugs.Add(slug))
            {
                continue;
            }

            result.Add((cleaned, slug));
        }

        return result;
    }
}
