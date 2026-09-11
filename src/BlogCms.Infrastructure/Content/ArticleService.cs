using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Content;

/// <summary>
/// Application service for article read/write operations, slug uniqueness,
/// tag management and the publish workflow.
/// </summary>
public interface IArticleService
{
    Task<(IReadOnlyList<Article> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, ArticleCategory? category = null, string? tagSlug = null,
        CancellationToken cancellationToken = default);

    Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Article>> GetForAdminAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default);

    Task<string> EnsureUniqueSlugAsync(string baseSlug, Guid? excludeArticleId, CancellationToken cancellationToken = default);

    Task<Article> CreateAsync(Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

    Task UpdateAsync(Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

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

    public async Task<(IReadOnlyList<Article> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, ArticleCategory? category = null, string? tagSlug = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Articles
            .AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published && a.PublishedAt != null);

        if (category is not null)
        {
            query = query.Where(a => a.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(tagSlug))
        {
            query = query.Where(a => a.ArticleTags.Any(at => at.Tag!.Slug == tagSlug));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .OrderByDescending(a => a.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.VideoEmbeds)
            .FirstOrDefaultAsync(
                a => a.Slug == slug && a.Status == ArticleStatus.Published && a.PublishedAt != null,
                cancellationToken);
    }

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Articles
            .Include(a => a.Author)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.VideoEmbeds)
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

    public async Task<IReadOnlyList<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
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

    public async Task<Article> CreateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        article.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(article.Slug) ? article.Title : article.Slug, null, cancellationToken);

        article.UpdatedAt = DateTime.UtcNow;
        if (article.Status == ArticleStatus.Published && article.PublishedAt is null)
        {
            article.PublishedAt = DateTime.UtcNow;
        }

        _db.Articles.Add(article);
        await _db.SaveChangesAsync(cancellationToken);

        await SyncTagsAsync(article, tagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return article;
    }

    public async Task UpdateAsync(
        Article article, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        article.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(article.Slug) ? article.Title : article.Slug, article.Id, cancellationToken);

        // PublishedAt is set once on first publish and never overwritten.
        if (article.Status == ArticleStatus.Published && article.PublishedAt is null)
        {
            article.PublishedAt = DateTime.UtcNow;
        }

        article.UpdatedAt = DateTime.UtcNow;

        _db.Articles.Update(article);
        await _db.SaveChangesAsync(cancellationToken);

        await SyncTagsAsync(article, tagNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
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
        var requested = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
}
