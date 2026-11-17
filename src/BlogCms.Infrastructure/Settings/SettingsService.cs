using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Infrastructure.Settings;

/// <summary>Stable keys for runtime-editable site settings.</summary>
public static class SettingKeys
{
    public const string ArticlesPerPage = "Display:ArticlesPerPage";
    public const int DefaultArticlesPerPage = 6;
    public const int MinArticlesPerPage = 1;
    public const int MaxArticlesPerPage = 50;
}

/// <summary>
/// Reads and writes runtime-editable display settings. Values are stored in the
/// <c>SiteSettings</c> table so administrators can change them without a redeploy.
/// </summary>
public interface ISettingsService
{
    Task<int> GetArticlesPerPageAsync(CancellationToken cancellationToken = default);

    Task SetArticlesPerPageAsync(int value, CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetArticlesPerPageAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _db.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.ArticlesPerPage, cancellationToken);

        if (setting is null || !int.TryParse(setting.Value, out var value))
        {
            return SettingKeys.DefaultArticlesPerPage;
        }

        return Math.Clamp(value, SettingKeys.MinArticlesPerPage, SettingKeys.MaxArticlesPerPage);
    }

    public async Task SetArticlesPerPageAsync(
        int value, CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(
            value, SettingKeys.MinArticlesPerPage, SettingKeys.MaxArticlesPerPage);

        var setting = await _db.SiteSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.ArticlesPerPage, cancellationToken);

        if (setting is null)
        {
            _db.SiteSettings.Add(new SiteSetting
            {
                Key = SettingKeys.ArticlesPerPage,
                Value = clamped.ToString(),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = clamped.ToString();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
