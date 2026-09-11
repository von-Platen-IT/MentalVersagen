namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Application-level email abstraction. Real implementations are config-driven
/// (e.g. Brevo/Mailjet via SMTP/API); the local development build uses
/// <see cref="DevEmailSender"/>, which writes messages to disk instead of
/// sending them, so the app runs without external credentials.
/// </summary>
public interface IAppEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
