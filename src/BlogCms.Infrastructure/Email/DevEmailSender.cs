using Microsoft.Extensions.Logging;

namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Development implementation of <see cref="IAppEmailSender"/> that writes each
/// message as an HTML file under <c>App_Data/emails</c> (next to the built app)
/// and logs its location, instead of contacting a mail provider. This keeps the
/// app runnable locally without any credentials — the confirmation link can be
/// copied straight out of the file.
/// </summary>
public sealed class DevEmailSender : IAppEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly string _outputDirectory;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
        _outputDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "emails");
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outputDirectory);

        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.html";
        var filePath = Path.Combine(_outputDirectory, fileName);

        var document =
            $"""
            <!-- DEV EMAIL - not actually sent -->
            <!-- To: {toEmail} -->
            <!-- Subject: {subject} -->
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8" /><title>{System.Net.WebUtility.HtmlEncode(subject)}</title></head>
            <body>
            {htmlBody}
            </body>
            </html>
            """;

        await File.WriteAllTextAsync(filePath, document, cancellationToken);

        _logger.LogInformation(
            "DEV email for {To} with subject '{Subject}' written to {Path}",
            toEmail, subject, filePath);
    }
}
