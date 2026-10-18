using ImageMagick;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Media;

public sealed record ProcessedImage(byte[] Data, string ContentType, string Extension, int Width, int Height);

/// <summary>
/// Validates and processes uploaded images (see 03-Medien-Upload-und-Embedding.md).
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Decodes the image from its actual content (not the file name), resizes it,
    /// optionally converts to WebP and re-encodes (which drops EXIF metadata).
    /// Returns null if the content is not a supported image.
    /// </summary>
    Task<ProcessedImage?> ProcessAsync(Stream input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Image processing backed by Magick.NET (Apache-2.0) — the free replacement for
/// the commercially licensed ImageSharp. Decodes from the actual content, applies
/// the EXIF orientation, resizes proportionally and re-encodes while stripping
/// all metadata (EXIF/GPS/ICC).
/// </summary>
public sealed class MagickImageProcessor : IImageProcessor
{
    private static readonly HashSet<MagickFormat> AllowedFormats =
    [
        MagickFormat.Jpeg,
        MagickFormat.Png,
        MagickFormat.WebP,
        MagickFormat.Gif
    ];

    private readonly MediaOptions _options;

    public MagickImageProcessor(IOptions<MediaOptions> options)
    {
        _options = options.Value;
    }

    public Task<ProcessedImage?> ProcessAsync(Stream input, CancellationToken cancellationToken = default)
    {
        // Magick.NET is CPU-bound and synchronous — run it on the thread pool so
        // the request thread is not blocked.
        return Task.Run(() => Process(input), cancellationToken);
    }

    private ProcessedImage? Process(Stream input)
    {
        try
        {
            using var image = new MagickImage(input);

            if (!AllowedFormats.Contains(image.Format))
            {
                return null;
            }

            // EXIF orientation first, then proportional resize (fit inside the
            // configured maximum edge), then strip all metadata.
            image.AutoOrient();

            // Only shrink — never upscale. MagickGeometry(max, max) would also
            // enlarge smaller images, unlike ImageSharp's former ResizeMode.Max.
            var maxEdge = (uint)_options.MaxEdgePixels;
            if (image.Width > maxEdge || image.Height > maxEdge)
            {
                image.Resize(new MagickGeometry(maxEdge, maxEdge));
            }

            image.Strip();

            var isGif = image.Format == MagickFormat.Gif;
            var targetFormat = _options.ConvertToWebP && !isGif ? MagickFormat.WebP : image.Format;

            using var output = new MemoryStream();
            image.Format = targetFormat;
            image.Write(output);

            var (contentType, extension) = MapContentType(targetFormat);
            return new ProcessedImage(
                output.ToArray(), contentType, extension, (int)image.Width, (int)image.Height);
        }
        catch (MagickException)
        {
            // Corrupt or unsupported content — the upload workflow reports a
            // friendly error instead of crashing.
            return null;
        }
    }

    private static (string ContentType, string Extension) MapContentType(MagickFormat format) =>
        format switch
        {
            MagickFormat.Png => ("image/png", "png"),
            MagickFormat.Gif => ("image/gif", "gif"),
            MagickFormat.WebP => ("image/webp", "webp"),
            _ => ("image/jpeg", "jpg")
        };
}
