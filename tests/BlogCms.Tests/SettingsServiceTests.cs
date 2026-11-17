using BlogCms.Infrastructure.Settings;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests for the runtime-editable display settings: default value, round-trip and
/// clamping to the supported range.
/// </summary>
public class SettingsServiceTests
{
    [Fact]
    public async Task GetArticlesPerPage_DefaultIsSix()
    {
        await using var db = TestDb.Create();
        var service = new SettingsService(db);

        Assert.Equal(6, await service.GetArticlesPerPageAsync());
    }

    [Fact]
    public async Task SetArticlesPerPage_RoundTrips()
    {
        await using var db = TestDb.Create();
        var service = new SettingsService(db);

        await service.SetArticlesPerPageAsync(12);

        Assert.Equal(12, await service.GetArticlesPerPageAsync());
    }

    [Fact]
    public async Task SetArticlesPerPage_ClampsToRange()
    {
        await using var db = TestDb.Create();
        var service = new SettingsService(db);

        await service.SetArticlesPerPageAsync(999);
        Assert.Equal(50, await service.GetArticlesPerPageAsync());

        await service.SetArticlesPerPageAsync(0);
        Assert.Equal(1, await service.GetArticlesPerPageAsync());
    }
}
