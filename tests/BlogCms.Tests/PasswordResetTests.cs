using BlogCms.Domain.Entities;
using BlogCms.Infrastructure.Data;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests für die Drosselung der Passwort-Reset-Anfragen. Die Implementierung
/// arbeitet rein im Speicher und benötigt keine Datenbank.
/// </summary>
public class PasswordResetThrottleTests
{
    private static MemoryCachePasswordResetThrottle Create() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void TryAcquire_AllowsUpToMaxAttempts_ThenBlocks()
    {
        var throttle = Create();

        for (var i = 0; i < MemoryCachePasswordResetThrottle.MaxAttempts; i++)
        {
            Assert.True(throttle.TryAcquire("192.0.2.1"));
        }

        Assert.False(throttle.TryAcquire("192.0.2.1"));
    }

    [Fact]
    public void TryAcquire_IsolatesDifferentClients()
    {
        var throttle = Create();

        for (var i = 0; i < MemoryCachePasswordResetThrottle.MaxAttempts; i++)
        {
            Assert.True(throttle.TryAcquire("192.0.2.1"));
        }

        // A different client must not inherit the exhausted budget.
        Assert.True(throttle.TryAcquire("192.0.2.2"));
    }
}

/// <summary>
/// End-to-End-Test des Identity-Token-Flows, auf dem die Reset-Seiten aufbauen:
/// Token erzeugen, Passwort ändern, altes Passwort ungültig, Token nicht
/// wiederverwendbar. Läuft mit EF-InMemory und ohne echtes SMTP.
/// </summary>
public class PasswordResetFlowTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("password-reset-" + Guid.NewGuid().ToString("N")));

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ResetToken_ChangesPassword_AndCannotBeReused()
    {
        await using var provider = BuildProvider();
        var userManager = provider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = "reset@example.com",
            Email = "reset@example.com",
            DisplayName = "Reset Test"
        };

        var created = await userManager.CreateAsync(user, "AltesPasswort1");
        Assert.True(created.Succeeded);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await userManager.ResetPasswordAsync(user, token, "NeuesPasswort2");
        Assert.True(reset.Succeeded);

        Assert.False(await userManager.CheckPasswordAsync(user, "AltesPasswort1"));
        Assert.True(await userManager.CheckPasswordAsync(user, "NeuesPasswort2"));

        // Identity invalidiert den Token nach erfolgreicher Verwendung.
        var reused = await userManager.ResetPasswordAsync(user, token, "NochEinPasswort3");
        Assert.False(reused.Succeeded);
    }

    [Fact]
    public async Task ResetToken_IsRejected_ForUnknownUser()
    {
        await using var provider = BuildProvider();
        var userManager = provider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = "unknown@example.com",
            Email = "unknown@example.com",
            DisplayName = "Unknown"
        };

        var created = await userManager.CreateAsync(user, "AltesPasswort1");
        Assert.True(created.Succeeded);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // Simuliert einen manipulierten Link: der Token gehört zu einem anderen
        // Konto und darf dort nicht akzeptiert werden.
        var other = new User
        {
            UserName = "other@example.com",
            Email = "other@example.com",
            DisplayName = "Other"
        };
        Assert.True((await userManager.CreateAsync(other, "AltesPasswort1")).Succeeded);

        var result = await userManager.ResetPasswordAsync(other, token, "NeuesPasswort2");
        Assert.False(result.Succeeded);
    }
}
