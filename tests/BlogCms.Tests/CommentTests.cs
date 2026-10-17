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
    public async Task EditAsync_FlagsComment_WhenEditedContentMatchesBlocklist()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var userId = await AddUserAsync(db);

        var created = await service.AddAsync(Guid.NewGuid(), userId, "harmloser Text", null);
        Assert.Equal(CommentStatus.Approved, created.Comment!.Status);

        var edited = await service.EditAsync(created.Comment.Id, userId, "Besuche unser casino jetzt");
        Assert.True(edited.Succeeded);
        Assert.Equal(CommentStatus.Flagged, edited.Comment!.Status);
    }

    [Fact]
    public async Task AddAsync_AllowsReplyToReply_OnSameArticle()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var articleId = Guid.NewGuid();

        var parent = await service.AddAsync(articleId, await AddUserAsync(db), "Eltern", null);
        var reply = await service.AddAsync(articleId, await AddUserAsync(db), "Antwort", parent.Comment!.Id);
        var replyToReply = await service.AddAsync(
            articleId, await AddUserAsync(db), "Antwort auf Antwort", reply.Comment!.Id);

        Assert.True(replyToReply.Succeeded);
        Assert.Equal(reply.Comment.Id, replyToReply.Comment!.ParentCommentId);
    }

    [Fact]
    public async Task AddAsync_RejectsReply_ToCommentOfOtherArticle()
    {
        var db = TestDb.Create();
        var service = Create(db);

        var foreign = await service.AddAsync(Guid.NewGuid(), await AddUserAsync(db), "Anderer Artikel", null);
        var result = await service.AddAsync(Guid.NewGuid(), await AddUserAsync(db), "Falsche Antwort", foreign.Comment!.Id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SoftDeleteAsync_AllowsArticleAuthor_ToDeleteForeignCommentOnOwnArticle()
    {
        var db = TestDb.Create();
        var service = Create(db);
        var articleAuthorId = await AddUserAsync(db);
        var commenterId = await AddUserAsync(db);

        var article = new Article
        {
            Id = Guid.NewGuid(),
            Title = "Meine Akte",
            Slug = "meine-akte",
            ContentMarkdown = "Text",
            Category = ArticleCategory.Politik,
            Status = ArticleStatus.Published,
            PublishedAt = DateTime.UtcNow,
            AuthorId = articleAuthorId
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var created = await service.AddAsync(article.Id, commenterId, "Fremder Kommentar", null);
        var deleted = await service.SoftDeleteAsync(created.Comment!.Id, articleAuthorId, isModerator: false);

        Assert.True(deleted);
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
        Assert.Contains(ReportReason.Spam, group.Reasons);
        Assert.Contains(ReportReason.Beleidigung, group.Reasons);
        Assert.Contains("note", group.Notes);

        // Moderating (approving) closes open reports automatically.
        await service.ModerateAsync(comment.Comment.Id, approve: true);
        Assert.Empty(await service.GetOpenReportsAsync());
    }
}
