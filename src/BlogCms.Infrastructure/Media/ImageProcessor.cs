using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

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

public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPEG", "PNG", "WEBP", "GIF"
    };

    private readonly MediaOptions _options;

    public ImageSharpImageProcessor(IOptions<MediaOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ProcessedImage?> ProcessAsync(Stream input, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync(input, cancellationToken);

            var format = image.Metadata.DecodedImageFormat;
            if (format is null || !AllowedFormats.Contains(format.Name))
            {
                return null;
            }

            // AutoOrient applies the EXIF orientation; the subsequent re-encode
            // does not carry EXIF over, so location metadata is removed.
            image.Mutate(context => context
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(_options.MaxEdgePixels, _options.MaxEdgePixels)
                }));

            using var output = new MemoryStream();
            var isGif = string.Equals(format.Name, "GIF", StringComparison.OrdinalIgnoreCase);

            if (_options.ConvertToWebP && !isGif)
            {
                await image.SaveAsWebpAsync(output, cancellationToken);
                return new ProcessedImage(output.ToArray(), "image/webp", "webp", image.Width, image.Height);
            }

            var encoder = CreateEncoder(format.Name);
            await image.SaveAsync(output, encoder, cancellationToken);

            var (contentType, extension) = MapContentType(format.Name);
            return new ProcessedImage(output.ToArray(), contentType, extension, image.Width, image.Height);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        catch (InvalidImageContentException)
        {
            return null;
        }
    }

    private static IImageEncoder CreateEncoder(string formatName) => formatName.ToUpperInvariant() switch
    {
        "PNG" => new PngEncoder(),
        "GIF" => new GifEncoder(),
        _ => new JpegEncoder()
    };

    private static (string ContentType, string Extension) MapContentType(string formatName) =>
        formatName.ToUpperInvariant() switch
        {
            "PNG" => ("image/png", "png"),
            "GIF" => ("image/gif", "gif"),
            "WEBP" => ("image/webp", "webp"),
            _ => ("image/jpeg", "jpg")
        };
}
