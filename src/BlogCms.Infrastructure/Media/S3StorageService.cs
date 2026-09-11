using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Media;

/// <summary>
/// Object storage backend for S3-compatible services (Cloudflare R2, MinIO, ...).
/// Activated when <c>Media:Provider</c> is "S3" and the S3 settings are present.
/// </summary>
public sealed class S3StorageService : IStorageService, IDisposable
{
    private readonly MediaOptions _options;
    private readonly AmazonS3Client _client;

    public S3StorageService(IOptions<MediaOptions> options)
    {
        _options = options.Value;

        var config = new AmazonS3Config
        {
            // Path-style works for R2/MinIO; harmless for most S3-compatible services.
            ForcePathStyle = true
        };

        if (!string.IsNullOrWhiteSpace(_options.S3ServiceUrl))
        {
            config.ServiceURL = _options.S3ServiceUrl;
        }

        _client = new AmazonS3Client(config)
        {
            // Credentials are supplied via options; the SDK picks them up from the request.
        };

        // Explicit credentials take precedence when configured.
        if (!string.IsNullOrWhiteSpace(_options.S3AccessKey) &&
            !string.IsNullOrWhiteSpace(_options.S3SecretKey))
        {
            _client.Dispose();
            _client = new AmazonS3Client(
                _options.S3AccessKey, _options.S3SecretKey, config);
        }
    }

    private string Bucket =>
        _options.S3Bucket ?? throw new InvalidOperationException("Media:S3Bucket is not configured.");

    public async Task<string> SaveAsync(
        Stream content, string key, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true
        };

        await _client.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(Bucket, key, cancellationToken);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(Bucket, key, cancellationToken);
    }

    public string GetPublicUrl(string key)
    {
        var baseUrl = _options.S3PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl.TrimEnd('/')}/{key}";
        }

        var serviceUrl = _options.S3ServiceUrl?.TrimEnd('/') ?? string.Empty;
        return $"{serviceUrl}/{Bucket}/{key}";
    }

    public void Dispose() => _client.Dispose();
}
