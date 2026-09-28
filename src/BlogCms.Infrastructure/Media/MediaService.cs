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

    /// <summary>
    /// Attaches previously uploaded, still unassigned article images (uploaded by the
    /// given user) to an article. Used by the Markdown editor: images are uploaded
    /// before the article exists (create) and claimed on save. Returns the count.
    /// </summary>
    Task<int> AttachToArticleAsync(
        IEnumerable<Guid> assetIds, Guid articleId, Guid uploadedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an asset from storage and the database. Returns false when not found.</summary>
    Task<bool> DeleteAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>Updates the alt text of an asset. Returns the updated asset or null.</summary>
    Task<MediaAsset?> SetAltTextAsync(Guid assetId, string? altText, CancellationToken cancellationToken = default);

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
            // Guid.Empty marks a still unassigned asset (e.g. editor upload before the
            // article exists); it is claimed later via AttachToArticleAsync.
            ArticleId = ownerType == MediaOwnerType.Article && ownerId != Guid.Empty ? ownerId : null,
            CommentId = ownerType == MediaOwnerType.Comment && ownerId != Guid.Empty ? ownerId : null,
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

    public async Task<int> AttachToArticleAsync(
        IEnumerable<Guid> assetIds, Guid articleId, Guid uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        // Only claim assets that are still unassigned and were uploaded by this user.
        var assets = await _db.MediaAssets
            .Where(m => ids.Contains(m.Id) &&
                        m.ArticleId == null &&
                        m.OwnerType == MediaOwnerType.Article &&
                        m.UploadedByUserId == uploadedByUserId)
            .ToListAsync(cancellationToken);

        var hasCover = await _db.MediaAssets.AnyAsync(m => m.ArticleId == articleId && m.IsCover, cancellationToken);
        for (var i = 0; i < assets.Count; i++)
        {
            assets[i].ArticleId = articleId;
            if (!hasCover && i == 0)
            {
                assets[i].IsCover = true;
                hasCover = true;
            }
        }

        if (assets.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return assets.Count;
    }

    public async Task<bool> DeleteAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return false;
        }

        await _storage.DeleteAsync(asset.StoragePath, cancellationToken);
        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MediaAsset?> SetAltTextAsync(Guid assetId, string? altText, CancellationToken cancellationToken = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        asset.AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public string GetUrl(MediaAsset asset) => _storage.GetPublicUrl(asset.StoragePath);
}
