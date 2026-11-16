using BlogCms.Domain.Categories;
using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Categories;

/// <summary>
/// Idempotently seeds the system category <c>undefined</c> and the three legacy
/// categories that used to live in the <c>ArticleCategory</c> enum. Existing
/// categories are left untouched, so admin edits survive restarts.
/// </summary>
public static class CategorySeeder
{
    public const string SatireDisclaimer =
        "Dieser Beitrag kann satirische Inhalte enthalten und unter Zuhilfenahme von KI erstellt " +
        "worden sein. Es handelt sich um ein Kunstwerk!";

    public const string VerschwoerungDisclaimer =
        "Dieser Beitrag behandelt verschwörungstheoretische Inhalte zur kritischen Auseinandersetzung " +
        "und dient nicht der Verbreitung von Falschinformationen.";

    public static async Task SeedAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        await EnsureAsync(db, CategoryService.CreateUndefined(), cancellationToken);

        await EnsureAsync(db, new Category
        {
            Id = CategoryIds.Politik,
            Name = "Politik",
            Slug = CategoryIds.PolitikSlug,
            DisplayOrder = 1
        }, cancellationToken);

        await EnsureAsync(db, new Category
        {
            Id = CategoryIds.Satire,
            Name = "Satire",
            Slug = CategoryIds.SatireSlug,
            Disclaimer = SatireDisclaimer,
            DisplayOrder = 2
        }, cancellationToken);

        await EnsureAsync(db, new Category
        {
            Id = CategoryIds.Verschwoerungstheorien,
            Name = "Verschwörungstheorien",
            Slug = CategoryIds.VerschwoerungstheorienSlug,
            Disclaimer = VerschwoerungDisclaimer,
            DisplayOrder = 3
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAsync(
        AppDbContext db, Category category, CancellationToken cancellationToken)
    {
        var exists = await db.Categories.AnyAsync(c => c.Id == category.Id, cancellationToken);
        if (!exists)
        {
            db.Categories.Add(category);
        }
    }
}