using BlogCms.Infrastructure.Media;
using ImageMagick;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests des Magick.NET-basierten Bild-Prozessors (Ersatz für ImageSharp):
/// Format-Whitelist, WebP-Konvertierung, proportionale Größenanpassung,
/// GIF-Sonderfall und robuste Behandlung ungültiger Inhalte.
/// </summary>
public class MagickImageProcessorTests
{
    private static MagickImageProcessor CreateProcessor(bool convertToWebP = true, int maxEdge = 1600) =>
        new(Options.Create(new MediaOptions { ConvertToWebP = convertToWebP, MaxEdgePixels = maxEdge }));

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new MagickImage(MagickColors.Red, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Png);
    }

    [Fact]
    public async Task Png_WithWebPConversion_ReturnsWebp()
    {
        var processor = CreateProcessor();
        using var input = new MemoryStream(CreatePng(100, 50));

        var result = await processor.ProcessAsync(input);

        Assert.NotNull(result);
        Assert.Equal("image/webp", result!.ContentType);
        Assert.Equal("webp", result.Extension);
        Assert.NotEmpty(result.Data);
        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public async Task Png_WithoutWebPConversion_ReturnsPng()
    {
        var processor = CreateProcessor(convertToWebP: false);
        using var input = new MemoryStream(CreatePng(100, 50));

        var result = await processor.ProcessAsync(input);

        Assert.NotNull(result);
        Assert.Equal("image/png", result!.ContentType);
        Assert.Equal("png", result.Extension);
    }

    [Fact]
    public async Task LargeImage_IsResizedProportionally()
    {
        var processor = CreateProcessor(convertToWebP: false, maxEdge: 1600);
        using var input = new MemoryStream(CreatePng(3200, 1600));

        var result = await processor.ProcessAsync(input);

        Assert.NotNull(result);
        Assert.Equal(1600, result!.Width);
        Assert.Equal(800, result.Height);
    }

    [Fact]
    public async Task Gif_StaysGifEvenWithWebPConversion()
    {
        var processor = CreateProcessor(convertToWebP: true);
        using var gif = new MagickImage(MagickColors.Blue, 40, 40);
        using var input = new MemoryStream(gif.ToByteArray(MagickFormat.Gif));

        var result = await processor.ProcessAsync(input);

        Assert.NotNull(result);
        Assert.Equal("image/gif", result!.ContentType);
        Assert.Equal("gif", result.Extension);
    }

    [Fact]
    public async Task InvalidContent_ReturnsNull()
    {
        var processor = CreateProcessor();
        using var input = new MemoryStream("kein bild"u8.ToArray());

        var result = await processor.ProcessAsync(input);

        Assert.Null(result);
    }
}
