using BlogCms.Infrastructure.Categories;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Tests;

internal static class TestDb
{
    public static AppDbContext Create()
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        // Articles reference the seeded categories (including "undefined"), so the
        // catalogue must exist before tests create articles.
        CategorySeeder.SeedAsync(db).GetAwaiter().GetResult();
        return db;
    }
}
