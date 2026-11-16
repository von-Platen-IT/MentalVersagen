using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Categories;

/// <summary>Editable fields of a category.</summary>
public sealed record CategoryInput(
    string Name,
    string? Slug,
    string? Description,
    string? Disclaimer,
    int DisplayOrder = 0);

/// <summary>Result of a category operation; failures carry a user-facing message.</summary>
public sealed record CategoryResult(bool Succeeded, Category? Category, string? Error)
{
    public static CategoryResult Ok(Category category) => new(true, category, null);

    public static CategoryResult Fail(string error) => new(false, null, error);
}

/// <summary>A category together with the number of articles assigned to it.</summary>
public sealed record CategoryUsage(Category Category, int ArticleCount);

/// <summary>
/// Application service for the admin-managed category catalogue. The built-in
/// <c>undefined</c> category is protected against deletion and renaming; deleting
/// any other category reassigns its articles to <c>undefined</c> first.
/// </summary>
public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>All categories with their article count (admin overview).</summary>
    Task<IReadOnlyList<CategoryUsage>> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Returns the system category <c>undefined</c>, creating it if missing.</summary>
    Task<Category> GetUndefinedAsync(CancellationToken cancellationToken = default);

    Task<CategoryResult> CreateAsync(CategoryInput input, CancellationToken cancellationToken = default);

    Task<CategoryResult> UpdateAsync(
        Guid id, CategoryInput input, CancellationToken cancellationToken = default);

    Task<CategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reassigns an article to another category (admin action).</summary>
    Task<CategoryResult> AssignArticleAsync(
        Guid articleId, Guid categoryId, CancellationToken cancellationToken = default);

    Task<int> CountArticlesAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public sealed class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ISlugGenerator _slugGenerator;

    public CategoryService(AppDbContext db, ISlugGenerator slugGenerator)
    {
        _db = db;
        _slugGenerator = slugGenerator;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        // Regular categories first (by display order then name), the system
        // "undefined" category last.
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.IsSystem ? 1 : 0)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryUsage>> GetUsageAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.IsSystem ? 1 : 0)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryUsage(c, c.Articles.Count))
            .ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Category?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = _slugGenerator.Generate(slug);
        if (string.IsNullOrEmpty(normalized))
        {
            return Task.FromResult<Category?>(null);
        }

        return _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == normalized, cancellationToken);
    }

    public async Task<Category> GetUndefinedAsync(CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == CategoryIds.Undefined, cancellationToken);

        if (category is null)
        {
            category = CreateUndefined();
            _db.Categories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return category;
    }

    public async Task<CategoryResult> CreateAsync(
        CategoryInput input, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(input.Name);
        if (name is null)
        {
            return CategoryResult.Fail("Bitte einen Kategorienamen angeben (max. 100 Zeichen).");
        }

        if (await NameExistsAsync(name, excludeId: null, cancellationToken))
        {
            return CategoryResult.Fail($"Eine Kategorie mit dem Namen „{name}“ existiert bereits.");
        }

        var slug = await EnsureUniqueSlugAsync(input.Slug, name, excludeId: null, cancellationToken);
        if (slug == CategoryIds.UndefinedSlug)
        {
            return CategoryResult.Fail("Der Slug „undefined“ ist der Systemkategorie vorbehalten.");
        }

        var category = new Category
        {
            Name = name,
            Slug = slug,
            Description = NormalizeOptional(input.Description, 500),
            Disclaimer = NormalizeOptional(input.Disclaimer, 1000),
            DisplayOrder = input.DisplayOrder > 0
                ? input.DisplayOrder
                : await NextDisplayOrderAsync(cancellationToken)
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> UpdateAsync(
        Guid id, CategoryInput input, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return CategoryResult.Fail("Die Kategorie wurde nicht gefunden.");
        }

        var name = NormalizeName(input.Name);
        if (name is null)
        {
            return CategoryResult.Fail("Bitte einen Kategorienamen angeben (max. 100 Zeichen).");
        }

        if (category.IsSystem)
        {
            var requestedSlug = string.IsNullOrWhiteSpace(input.Slug)
                ? category.Slug
                : _slugGenerator.Generate(input.Slug);

            if (!string.Equals(category.Name, name, StringComparison.Ordinal) ||
                !string.Equals(category.Slug, requestedSlug, StringComparison.Ordinal))
            {
                return CategoryResult.Fail(
                    "Die Systemkategorie „undefined“ kann nicht umbenannt werden.");
            }

            // Description, disclaimer and display order remain editable.
            category.Description = NormalizeOptional(input.Description, 500);
            category.Disclaimer = NormalizeOptional(input.Disclaimer, 1000);
            category.DisplayOrder = input.DisplayOrder;
            await _db.SaveChangesAsync(cancellationToken);
            return CategoryResult.Ok(category);
        }

        if (await NameExistsAsync(name, excludeId: id, cancellationToken))
        {
            return CategoryResult.Fail($"Eine Kategorie mit dem Namen „{name}“ existiert bereits.");
        }

        // The slug is stable across renames unless the admin explicitly supplies a
        // new one. That keeps public filter URLs working.
        var slug = string.IsNullOrWhiteSpace(input.Slug)
            ? category.Slug
            : await EnsureUniqueSlugAsync(input.Slug, name, excludeId: id, cancellationToken);

        if (slug == CategoryIds.UndefinedSlug)
        {
            return CategoryResult.Fail("Der Slug „undefined“ ist der Systemkategorie vorbehalten.");
        }

        category.Name = name;
        category.Slug = slug;
        category.Description = NormalizeOptional(input.Description, 500);
        category.Disclaimer = NormalizeOptional(input.Disclaimer, 1000);
        category.DisplayOrder = input.DisplayOrder > 0 ? input.DisplayOrder : category.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> DeleteAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return CategoryResult.Fail("Die Kategorie wurde nicht gefunden.");
        }

        if (category.IsSystem || id == CategoryIds.Undefined)
        {
            return CategoryResult.Fail(
                "Die Systemkategorie „undefined“ kann nicht gelöscht werden.");
        }

        // Two-phase save inside one transaction: reassign the articles first, then
        // delete the category. This keeps the FK (DeleteBehavior.Restrict) happy
        // regardless of EF's command ordering.
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var affected = await _db.Articles
                .Where(a => a.CategoryId == id)
                .ToListAsync(cancellationToken);

            foreach (var article in affected)
            {
                article.CategoryId = CategoryIds.Undefined;
                article.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync(cancellationToken);

            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (tx is not null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            throw;
        }

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> AssignArticleAsync(
        Guid articleId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var article = await _db.Articles
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);
        if (article is null)
        {
            return CategoryResult.Fail("Der Beitrag wurde nicht gefunden.");
        }

        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryResult.Fail("Die Kategorie wurde nicht gefunden.");
        }

        article.CategoryId = categoryId;
        article.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return CategoryResult.Ok(category);
    }

    public Task<int> CountArticlesAsync(
        Guid categoryId, CancellationToken cancellationToken = default) =>
        _db.Articles.CountAsync(a => a.CategoryId == categoryId, cancellationToken);

    internal static Category CreateUndefined() => new()
    {
        Id = CategoryIds.Undefined,
        Name = "undefined",
        Slug = CategoryIds.UndefinedSlug,
        Description = "Beiträge ohne ausdrückliche Kategorie.",
        DisplayOrder = 1000,
        IsSystem = true
    };

    private async Task<bool> NameExistsAsync(
        string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLowerInvariant();
        return await _db.Categories.AnyAsync(
            c => c.Name.ToLower() == normalized && (excludeId == null || c.Id != excludeId),
            cancellationToken);
    }

    private async Task<string> EnsureUniqueSlugAsync(
        string? requestedSlug, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var baseSlug = _slugGenerator.Generate(
            string.IsNullOrWhiteSpace(requestedSlug) ? name : requestedSlug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "kategorie";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Categories.AnyAsync(
                   c => c.Slug == slug && (excludeId == null || c.Id != excludeId),
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private async Task<int> NextDisplayOrderAsync(CancellationToken cancellationToken)
    {
        var max = await _db.Categories
            .Where(c => !c.IsSystem)
            .MaxAsync(c => (int?)c.DisplayOrder, cancellationToken);

        return (max ?? 0) + 1;
    }

    private static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 100 ? null : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}