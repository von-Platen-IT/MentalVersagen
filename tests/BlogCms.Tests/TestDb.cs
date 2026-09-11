using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Tests;

internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
