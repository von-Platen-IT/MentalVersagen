using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class CommentServiceTests
{
    private static CommentService Create(
        AppDbContext db, CommentOptions? options = null) =>
        new(db, Options.Create(options ?? new CommentOptions()));

    /// <summary>Comments have a required author, so tests must persist real users.</summary>
    private static async Task<Guid> AddUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"{Guid.NewGuid():N}@example.com",
            Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = "Testnutzer"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task AddAsync_ApprovesByDefault_InPostModeration()
    {
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.AddAsync(Guid.NewGuid(), await AddUserAsync(db), "Ein normaler Kommentar", null);

        Assert.True(result.Succeeded);
        Assert.Equal(CommentStatus.Approved, result.Comment!.Status);
    }

    [Fact]
    public async Task AddAsync_FlagsBlocklistedContent()
    {
        // Acceptance criterion: automated filter flags matches, does not publish them.
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.AddAsync(Guid.NewGuid(), await AddUserAsync(db), "Besuche unser casino jetzt", null);

        Assert.True(result.Succeeded);
        Assert.Equal(CommentStatus.Flagged, result.Comment!.Status);
    }

    [Fact]
    public async Task AddAsync_EnforcesRateLimit()
    {
        // Acceptance criterion: rate limiting kicks in past the threshold.
        var db = TestDb.Create();
        var service = Create(db, new CommentOptions { MaxCommentsPerMinute = 2 });
        var userId = await AddUserAsync(db);
        var articleId = Guid.NewGuid();

        Assert.True((await service.AddAsync(articleId, userId, "eins", null)).Succeeded);
        Assert.True((await service.AddAsync(articleId, userId, "zwei", null)).Succeeded);

        var third = await service.AddAsync(articleId, userId, "drei", null);

        Assert.False(third.Succeeded);
    }

    [Fact]
    public async Task EditAsync_RejectsAfterEditWindow()
    {
        var db = TestDb.Create();
        var service = Create(db, new CommentOptions { EditWindowMinutes = 0 });
        var userId = await AddUserAsync(db);

        var created = await service.AddAsync(Guid.NewGuid(), userId, "original", null);
        var edit = await service.EditAsync(created.Comment!.Id, userId, "geändert");

        Assert.False(edit.Succeeded);
    }

    [Fact]
    public async Task SoftDeleteAsync_KeepsThreadStructure()
    {
        // Acceptance criterion: deleting a parent keeps replies visible.
        var db = TestDb.Create();
        var service = Create(db);
        var articleId = Guid.NewGuid();

        var parent = await service.AddAsync(articleId, await AddUserAsync(db), "Eltern", null);
        await service.AddAsync(articleId, await AddUserAsync(db), "Antwort", parent.Comment!.Id);

        var deleted = await service.SoftDeleteAsync(parent.Comment.Id, parent.Comment.UserId, isModerator: false);

        Assert.True(deleted);

        var thread = await service.GetThreadAsync(articleId);
        Assert.Equal(2, thread.Count);
        Assert.Contains(thread, c => c.DeletedAt is not null);
        Assert.Contains(thread, c => c.ParentCommentId == parent.Comment.Id);
    }

    [Fact]
    public async Task ReportAsync_CreatesReport_AndGroupsByCount()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var articleId = Guid.NewGuid();

        var comment = await service.AddAsync(articleId, await AddUserAsync(db), "umstritten", null);
        await service.ReportAsync(comment.Comment!.Id, await AddUserAsync(db), ReportReason.Spam, null);
        await service.ReportAsync(comment.Comment.Id, await AddUserAsync(db), ReportReason.Beleidigung, "note");

        var groups = await service.GetOpenReportsAsync();

        var group = Assert.Single(groups);
        Assert.Equal(2, group.ReportCount);
    }
}
