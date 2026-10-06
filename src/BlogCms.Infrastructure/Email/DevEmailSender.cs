using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Development implementation of <see cref="IAppEmailSender"/> that writes each
/// message as an HTML file under <c>App_Data/emails</c> (next to the built app)
/// and logs its location, instead of contacting a mail provider. This keeps the
/// app runnable locally without any credentials — the confirmation link can be
/// copied straight out of the file.
/// Writing the file must never fail the caller: in the container this code once
/// ran as a non-root user against a root-owned directory and turned a successful
/// registration into a 500 error page (see docs/04-mailversand.md).
/// </summary>
public sealed class DevEmailSender : IAppEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly string _outputDirectory;

    public DevEmailSender(ILogger<DevEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _outputDirectory = string.IsNullOrWhiteSpace(options.Value.DevOutputPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "emails")
            : options.Value.DevOutputPath;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (Exception ex)
        {
            // Swallowed on purpose: this sender is the fallback for local development
            // and must not turn a working registration into an error page.
            _logger.LogWarning(
                ex,
                "DEV email for {To} with subject '{Subject}' could not be written to {Path}.",
                toEmail, subject, _outputDirectory);
        }
    }
}
