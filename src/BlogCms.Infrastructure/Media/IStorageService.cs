namespace BlogCms.Infrastructure.Media;

/// <summary>
/// Abstraction over the object storage backend. Image binaries live in storage,
/// never in the application server's database (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public interface IStorageService
{
    /// <summary>Saves content under the given key and returns the key.</summary>
    Task<string> SaveAsync(
        Stream content, string key, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Resolves a publicly accessible URL (CDN / static path) for a key.</summary>
    string GetPublicUrl(string key);
}
