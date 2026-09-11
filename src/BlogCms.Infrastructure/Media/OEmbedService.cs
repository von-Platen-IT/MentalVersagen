using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BlogCms.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BlogCms.Infrastructure.Media;

public sealed record OEmbedResult(
    VideoPlatform Platform,
    bool IsEmbeddable,
    string? EmbedHtml,
    string? ThumbnailUrl);

/// <summary>
/// Resolves external video URLs to embed code via the platform's oEmbed endpoint,
/// with server-side caching (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public interface IOEmbedService
{
    VideoPlatform DetectPlatform(string url);

    Task<OEmbedResult> ResolveAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class OEmbedService : IOEmbedService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OEmbedService> _logger;

    public OEmbedService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<OEmbedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public VideoPlatform DetectPlatform(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return VideoPlatform.Other;
        }

        var value = url.ToLowerInvariant();

        if (value.Contains("youtube.com") || value.Contains("youtu.be"))
        {
            return VideoPlatform.YouTube;
        }

        if (value.Contains("vimeo.com"))
        {
            return VideoPlatform.Vimeo;
        }

        if (value.Contains("twitter.com") || value.Contains("//x.com"))
        {
            return VideoPlatform.X;
        }

        if (value.Contains("tiktok.com"))
        {
            return VideoPlatform.TikTok;
        }

        return VideoPlatform.Other;
    }

    public async Task<OEmbedResult> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        var platform = DetectPlatform(url);

        // Unknown patterns are not embedded, just linked.
        if (platform == VideoPlatform.Other)
        {
            return new OEmbedResult(VideoPlatform.Other, false, null, null);
        }

        var cacheKey = $"oembed:{url}";
        if (_cache.TryGetValue(cacheKey, out OEmbedResult? cached) && cached is not null)
        {
            return cached;
        }

        var endpoint = platform switch
        {
            VideoPlatform.YouTube => $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json",
            VideoPlatform.Vimeo => $"https://vimeo.com/api/oembed.json?url={Uri.EscapeDataString(url)}",
            VideoPlatform.X => $"https://publish.twitter.com/oembed?url={Uri.EscapeDataString(url)}",
            VideoPlatform.TikTok => $"https://www.tiktok.com/oembed?url={Uri.EscapeDataString(url)}",
            _ => null
        };

        if (endpoint is null)
        {
            return new OEmbedResult(platform, false, null, null);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("oembed");
            using var response = await client.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("oEmbed request for {Url} failed with {Status}", url, response.StatusCode);
                return new OEmbedResult(platform, false, null, null);
            }

            var payload = await response.Content.ReadFromJsonAsync<OEmbedResponse>(cancellationToken);
            var result = new OEmbedResult(
                platform,
                !string.IsNullOrWhiteSpace(payload?.Html),
                payload?.Html,
                payload?.ThumbnailUrl);

            // Cache server-side so the embed is not fetched on every page view.
            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "oEmbed request for {Url} threw an exception.", url);
            return new OEmbedResult(platform, false, null, null);
        }
    }

    private sealed class OEmbedResponse
    {
        [JsonPropertyName("html")]
        public string? Html { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }
    }
}
