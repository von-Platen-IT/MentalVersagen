using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Media;

/// <summary>
/// Development storage backend that writes files to a local directory served as
/// static content. Used when <c>Media:Provider</c> is "Local" (the default), so
/// the app runs without any object-storage credentials.
/// </summary>
public sealed class LocalDiskStorageService : IStorageService
{
    private readonly MediaOptions _options;
    private readonly ILogger<LocalDiskStorageService> _logger;

    public LocalDiskStorageService(IOptions<MediaOptions> options, ILogger<LocalDiskStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private string RootPath =>
        string.IsNullOrWhiteSpace(_options.LocalRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "uploads")
            : _options.LocalRootPath;

    public async Task<string> SaveAsync(
        Stream content, string key, string contentType, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(RootPath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Stored media asset locally at {Path}", fullPath);
        return key;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(RootPath, key);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(RootPath, key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        var baseUrl = _options.LocalPublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{key.Replace('\\', '/')}";
    }
}
