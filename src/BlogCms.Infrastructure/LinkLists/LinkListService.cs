using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.LinkLists;

public sealed record LinkListResult(bool Succeeded, string? Error, LinkList? LinkList = null)
{
    public static LinkListResult Ok(LinkList linkList) => new(true, null, linkList);

    public static LinkListResult Fail(string error) => new(false, error);
}

/// <summary>
/// Administrative management of editorially curated, ordered article lists
/// (FeatureFix1 BR-090/BR-091/BR-092).
/// </summary>
public interface ILinkListService
{
    Task<IReadOnlyList<LinkList>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<LinkList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LinkList?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<LinkListResult> CreateAsync(
        string title, string? slug, string? description, CancellationToken cancellationToken = default);

    Task<LinkListResult> UpdateAsync(
        Guid id, string title, string? slug, string? description, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Replaces the list content with the given articles, preserving the given order.</summary>
    Task SetItemsAsync(
        Guid linkListId, IEnumerable<Guid> articleIds, CancellationToken cancellationToken = default);
}

public sealed class LinkListService : ILinkListService
{
    private readonly AppDbContext _db;
    private readonly ISlugGenerator _slugGenerator;

    public LinkListService(AppDbContext db, ISlugGenerator slugGenerator)
    {
        _db = db;
        _slugGenerator = slugGenerator;
    }

    public async Task<IReadOnlyList<LinkList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.LinkLists
            .AsNoTracking()
            .Include(l => l.Items)
            .OrderBy(l => l.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<LinkList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.LinkLists
            .Include(l => l.Items).ThenInclude(i => i.Article)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public Task<LinkList?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return _db.LinkLists
            .AsNoTracking()
            .Include(l => l.Items).ThenInclude(i => i.Article)
            .FirstOrDefaultAsync(l => l.Slug == slug, cancellationToken);
    }

    public async Task<LinkListResult> CreateAsync(
        string title, string? slug, string? description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return LinkListResult.Fail("Titel ist erforderlich.");
        }

        var linkList = new LinkList
        {
            Title = title.Trim(),
            Slug = await EnsureUniqueSlugAsync(slug, title, null, cancellationToken),
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _db.LinkLists.Add(linkList);
        await _db.SaveChangesAsync(cancellationToken);

        return LinkListResult.Ok(linkList);
    }

    public async Task<LinkListResult> UpdateAsync(
        Guid id, string title, string? slug, string? description, CancellationToken cancellationToken = default)
    {
        var linkList = await _db.LinkLists.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (linkList is null)
        {
            return LinkListResult.Fail("Linkliste nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return LinkListResult.Fail("Titel ist erforderlich.");
        }

        linkList.Title = title.Trim();
        linkList.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(slug) ? linkList.Slug : slug, title, id, cancellationToken);
        linkList.Description = description;

        await _db.SaveChangesAsync(cancellationToken);
        return LinkListResult.Ok(linkList);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linkList = await _db.LinkLists.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (linkList is null)
        {
            return false;
        }

        _db.LinkLists.Remove(linkList);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetItemsAsync(
        Guid linkListId, IEnumerable<Guid> articleIds, CancellationToken cancellationToken = default)
    {
        var existing = await _db.LinkListItems
            .Where(i => i.LinkListId == linkListId)
            .ToListAsync(cancellationToken);

        _db.LinkListItems.RemoveRange(existing);

        var position = 0;
        foreach (var articleId in articleIds.Distinct())
        {
            var articleExists = await _db.Articles.AnyAsync(a => a.Id == articleId, cancellationToken);
            if (!articleExists)
            {
                continue;
            }

            _db.LinkListItems.Add(new LinkListItem
            {
                LinkListId = linkListId,
                ArticleId = articleId,
                Position = position++,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> EnsureUniqueSlugAsync(
        string? slug, string fallbackTitle, Guid? excludeId, CancellationToken cancellationToken)
    {
        var baseSlug = _slugGenerator.Generate(string.IsNullOrWhiteSpace(slug) ? fallbackTitle : slug);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "liste";
        }

        var candidate = baseSlug;
        var suffix = 2;

        while (await _db.LinkLists.AnyAsync(
                   l => l.Slug == candidate && (excludeId == null || l.Id != excludeId),
                   cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }
}
