namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Application-level email abstraction. Real implementations are config-driven
/// (e.g. Brevo/Mailjet via SMTP/API); the local development build uses
/// <see cref="DevEmailSender"/>, which writes messages to disk instead of
/// sending them, so the app runs without external credentials.
/// </summary>
public interface IAppEmailSender
{
    /// <summary>
    /// Sends an HTML e-mail. <paramref name="replyTo"/> is optional and lets
    /// forms (e.g. the contact form) route replies to the visitor's address
    /// while the technical sender stays the configured From address.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        string? replyTo = null);
}
