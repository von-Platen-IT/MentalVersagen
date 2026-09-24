using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Media;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Verifies the upload size limits enforced by <see cref="MediaService"/> —
/// in particular the 4 MB limit for comment images.
/// </summary>
public class MediaUploadLimitTests
{
    private sealed class FakeImageProcessor : IImageProcessor
    {
        public Task<ProcessedImage?> ProcessAsync(Stream input, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessedImage?>(new ProcessedImage([1, 2, 3], "image/webp", "webp", 10, 10));
    }

    private sealed class FakeStorage : IStorageService
    {
        public Task<string> SaveAsync(
            Stream content, string key, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(key);

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string GetPublicUrl(string key) => "/uploads/" + key;
    }

    private static MediaService Create(AppDbContext db, MediaOptions options) =>
        new(db, new FakeImageProcessor(), new FakeStorage(), Options.Create(options));

    [Fact]
    public void CommentMaxBytes_DefaultsToFourMegabytes()
    {
        Assert.Equal(4 * 1024 * 1024, new MediaOptions().CommentMaxBytes);
    }

    [Fact]
    public async Task UploadAsync_RejectsCommentImageAboveTheLimit()
    {
        using var db = TestDb.Create();
        var options = new MediaOptions();
        var service = Create(db, options);

        // One byte above the 4 MB limit.
        using var stream = new MemoryStream(new byte[options.CommentMaxBytes + 1]);

        var result = await service.UploadAsync(
            stream, "zu-gross.png", MediaOwnerType.Comment, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.False(result.Succeeded);
        Assert.Contains("zu groß", result.Error);
    }

    [Fact]
    public async Task UploadAsync_AcceptsCommentImageExactlyAtTheLimit()
    {
        using var db = TestDb.Create();
        var options = new MediaOptions();
        var service = Create(db, options);

        using var stream = new MemoryStream(new byte[options.CommentMaxBytes]);

        var result = await service.UploadAsync(
            stream, "grenzwertig.png", MediaOwnerType.Comment, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UploadAsync_RejectsEmptyFile()
    {
        using var db = TestDb.Create();
        var service = Create(db, new MediaOptions());

        using var stream = new MemoryStream();

        var result = await service.UploadAsync(
            stream, "leer.png", MediaOwnerType.Comment, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.False(result.Succeeded);
        Assert.Contains("leer", result.Error);
    }
}
