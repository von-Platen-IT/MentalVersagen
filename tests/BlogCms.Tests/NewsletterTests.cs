using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Newsletter;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class NewsletterServiceTests
{
    private static NewsletterService Create(AppDbContext db) =>
        new(db, new EphemeralDataProtectionProvider(), Options.Create(new NewsletterOptions()));

    [Fact]
    public async Task Subscribe_RequiresConfirmation_BeforeConfirmedAtIsSet()
    {
        // Acceptance criterion: entry without confirmation stays unconfirmed.
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.SubscribeAsync("test@example.com", null);

        Assert.NotNull(result.Subscriber);
        Assert.Null(result.Subscriber.ConfirmedAt);
        Assert.False(result.AlreadyConfirmed);
    }

    [Fact]
    public async Task Confirm_SetsConfirmedAt_ForValidToken()
    {
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.SubscribeAsync("test@example.com", null);
        var confirmed = await service.ConfirmAsync(result.Token);

        Assert.True(confirmed);
        var subscriber = await db.NewsletterSubscribers.FindAsync(result.Subscriber.Id);
        Assert.NotNull(subscriber!.ConfirmedAt);
    }

    [Fact]
    public async Task Unsubscribe_WorksWithoutLogin_ViaToken_AndKeepsRow()
    {
        // Acceptance criterion: unsubscribe works via token, no hard delete.
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.SubscribeAsync("test@example.com", null);
        await service.ConfirmAsync(result.Token);

        var unsubscribed = await service.UnsubscribeAsync(result.Token);

        Assert.True(unsubscribed);
        var subscriber = await db.NewsletterSubscribers.FindAsync(result.Subscriber.Id);
        Assert.NotNull(subscriber);
        Assert.NotNull(subscriber!.UnsubscribedAt);
    }

    [Fact]
    public async Task Confirm_RejectsInvalidToken()
    {
        var db = TestDb.Create();
        var service = Create(db);

        Assert.False(await service.ConfirmAsync("not-a-valid-token"));
    }

    [Fact]
    public async Task DeleteExpiredUnconfirmed_RemovesOldUnconfirmedEntries()
    {
        var db = TestDb.Create();
        var service = Create(db);

        var result = await service.SubscribeAsync("old@example.com", null);
        result.Subscriber.CreatedAt = DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();

        var removed = await service.DeleteExpiredUnconfirmedAsync();

        Assert.Equal(1, removed);
    }
}
