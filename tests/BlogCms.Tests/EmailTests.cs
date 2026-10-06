using BlogCms.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests für den Mailversand (docs/04-mailversand.md). Alle Fälle laufen
/// netzunabhängig: es wird sich nie in echtes SMTP eingeloggt.
/// </summary>
public class EmailOptionsTests
{
    private static EmailOptions FullyConfigured() => new()
    {
        Provider = "Smtp",
        Host = "smtp.ionos.com",
        Port = 587,
        UseStartTls = true,
        UserName = "noreply@mentalversagen.de",
        Password = "irrelevant-fuer-den-test",
        FromAddress = "noreply@mentalversagen.de",
        FromDisplayName = "MentalVersagen"
    };

    [Fact]
    public void IsConfigured_IsFalse_WhenPasswordIsMissing()
    {
        // Acceptance criterion: without credentials the dev fallback stays active,
        // so the app starts without any mail setup (A1).
        var options = FullyConfigured();
        options.Password = string.Empty;

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("Dev")]
    [InlineData("dev")]
    [InlineData("HttpApi")]
    [InlineData("")]
    public void IsConfigured_IsFalse_WhenProviderIsNotSmtp(string provider)
    {
        var options = FullyConfigured();
        options.Provider = provider;

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("Smtp")]
    [InlineData("smtp")]
    [InlineData("SMTP")]
    public void IsConfigured_IgnoresProviderCasing(string provider)
    {
        // Same behaviour as the Media/Payment options: the provider name is
        // compared case-insensitively, so a typo in casing cannot silently
        // disable the production mail path.
        var options = FullyConfigured();
        options.Provider = provider;

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_IsTrue_WithCompleteSmtpConfiguration()
    {
        Assert.True(FullyConfigured().IsConfigured);
    }

    [Fact]
    public void SectionName_IsEmail()
    {
        Assert.Equal("Email", EmailOptions.SectionName);
    }
}

public class DevEmailSenderTests
{
    private static DevEmailSender Create(string outputPath) =>
        new(
            NullLogger<DevEmailSender>.Instance,
            Options.Create(new EmailOptions { DevOutputPath = outputPath }));

    [Fact]
    public async Task SendAsync_WritesFile_IntoConfiguredDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mv-email-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Create(directory).SendAsync("test@example.com", "Betreff", "<p>Hallo</p>");

            var files = Directory.GetFiles(directory, "*.html");
            Assert.Single(files);
            Assert.Contains("test@example.com", await File.ReadAllTextAsync(files[0]));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SendAsync_DoesNotThrow_WhenDirectoryCannotBeCreated()
    {
        // Regression test for the production failure: in the container the sender
        // ran as non-root against a root-owned directory and turned a successful
        // registration into a 500 page (docs/04-mailversand.md, Abschnitt 1).
        var blocker = Path.Combine(Path.GetTempPath(), "mv-email-block-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blocker, "ich bin eine Datei, kein Verzeichnis");

        try
        {
            // The parent of the target is a file, so CreateDirectory must fail.
            var unwritable = Path.Combine(blocker, "emails");

            var exception = await Record.ExceptionAsync(
                () => Create(unwritable).SendAsync("test@example.com", "Betreff", "<p>Hallo</p>"));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WithUnreachableHost_ThrowsInsteadOfHanging()
    {
        // No network dependency: an unresolvable host must end in an exception
        // (bounded by the sender's own timeout), never in a hanging request.
        var options = new EmailOptions
        {
            Provider = "Smtp",
            Host = "smtp.invalid-host-for-tests.local",
            Port = 587,
            UseStartTls = true,
            UserName = "noreply@mentalversagen.de",
            Password = "irrelevant-fuer-den-test",
            FromAddress = "noreply@mentalversagen.de",
            FromDisplayName = "MentalVersagen"
        };

        var sender = new SmtpEmailSender(NullLogger<SmtpEmailSender>.Instance, Options.Create(options));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var exception = await Record.ExceptionAsync(
            () => sender.SendAsync("test@example.com", "Betreff", "<p>Hallo</p>", cts.Token));

        Assert.NotNull(exception);
    }
}
