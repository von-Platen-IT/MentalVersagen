using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Media;

public sealed record MediaUploadResult(bool Succeeded, string? Error, MediaAsset? Asset = null)
{
    public static MediaUploadResult Ok(MediaAsset asset) => new(true, null, asset);

    public static MediaUploadResult Fail(string error) => new(false, error);
}

public interface IMediaService
{
    Task<MediaUploadResult> UploadAsync(
        Stream file, string fileName, MediaOwnerType ownerType, Guid ownerId, Guid uploadedByUserId,
        string? altText, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaAsset>> GetForOwnerAsync(
        MediaOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default);

    string GetUrl(MediaAsset asset);
}

public sealed class MediaService : IMediaService
{
    private readonly AppDbContext _db;
    private readonly IImageProcessor _imageProcessor;
    private readonly IStorageService _storage;
    private readonly MediaOptions _options;

    public MediaService(
        AppDbContext db,
        IImageProcessor imageProcessor,
        IStorageService storage,
        IOptions<MediaOptions> options)
    {
        _db = db;
        _imageProcessor = imageProcessor;
        _storage = storage;
        _options = options.Value;
    }

    public async Task<MediaUploadResult> UploadAsync(
        Stream file, string fileName, MediaOwnerType ownerType, Guid ownerId, Guid uploadedByUserId,
        string? altText, CancellationToken cancellationToken = default)
    {
        var maxBytes = ownerType == MediaOwnerType.Article
            ? _options.ArticleMaxBytes
            : _options.CommentMaxBytes;

        // Read into memory with a hard size cap (comment uploads are limited tighter).
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await file.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                var limitMb = maxBytes / (1024.0 * 1024.0);
                return MediaUploadResult.Fail($"Die Datei ist zu groß (Limit: {limitMb:0.#} MB).");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            return MediaUploadResult.Fail("Die Datei ist leer.");
        }

        buffer.Position = 0;

        // Validation and processing happen on the actual content, not the file name.
        var processed = await _imageProcessor.ProcessAsync(buffer, cancellationToken);
        if (processed is null)
        {
            return MediaUploadResult.Fail("Ungültiges Bild. Erlaubt sind JPEG, PNG, WebP und GIF.");
        }

        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{processed.Extension}";

        using (var content = new MemoryStream(processed.Data))
        {
            await _storage.SaveAsync(content, key, processed.ContentType, cancellationToken);
        }

        var asset = new MediaAsset
        {
            OwnerType = ownerType,
            ArticleId = ownerType == MediaOwnerType.Article ? ownerId : null,
            CommentId = ownerType == MediaOwnerType.Comment ? ownerId : null,
            UploadedByUserId = uploadedByUserId,
            StoragePath = key,
            MimeType = processed.ContentType,
            FileSizeBytes = processed.Data.Length,
            Width = processed.Width,
            Height = processed.Height,
            AltText = altText,
            CreatedAt = DateTime.UtcNow
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return MediaUploadResult.Ok(asset);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetForOwnerAsync(
        MediaOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        return ownerType == MediaOwnerType.Article
            ? await _db.MediaAssets.AsNoTracking().Where(m => m.ArticleId == ownerId).ToListAsync(cancellationToken)
            : await _db.MediaAssets.AsNoTracking().Where(m => m.CommentId == ownerId).ToListAsync(cancellationToken);
    }

    public string GetUrl(MediaAsset asset) => _storage.GetPublicUrl(asset.StoragePath);
}
