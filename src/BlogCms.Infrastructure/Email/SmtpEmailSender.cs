using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Production implementation of <see cref="IAppEmailSender"/> that actually hands
/// the message to an SMTP server (MailKit). Everything — host, port, credentials,
/// sender — comes from the "Email" configuration section, so switching mail
/// providers never requires a code change (see docs/04-mailversand.md).
/// </summary>
public sealed class SmtpEmailSender : IAppEmailSender
{
    /// <summary>
    /// Hard upper bound for a single send. A hanging SMTP server must never block
    /// the HTTP request that triggered the mail.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly EmailOptions _options;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _options.FromDisplayName,
            _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)Timeout.TotalMilliseconds
        };

        // CancellationToken plus MailKit's own timeout: whichever fires first wins,
        // so a black-holed server ends in an exception instead of a hanging request.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(Timeout);

        try
        {
            var socketOptions = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(
                _options.Host, _options.Port, socketOptions, timeoutSource.Token);

            await client.AuthenticateAsync(
                _options.UserName, _options.Password, timeoutSource.Token);

            await client.SendAsync(message, timeoutSource.Token);

            // The client is reusable but its connection is not: close the socket
            // after every send instead of keeping a long-lived one open.
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, timeoutSource.Token);
            }

            _logger.LogInformation(
                "E-Mail '{Subject}' an {To} über {Host}:{Port} versendet.",
                subject, toEmail, _options.Host, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The exception itself may embed the credentials (SmtpCommandFailedException,
            // AuthenticationException, …), so it is deliberately NOT passed to the logger.
            // Only host/port and the exception type name — never the message.
            _logger.LogError(
                "E-Mail-Versand an {To} über {Host}:{Port} fehlgeschlagen ({ErrorType}).",
                toEmail,
                _options.Host,
                _options.Port,
                ex.GetType().Name);

            throw new EmailDeliveryException(
                $"SMTP-Versand an {toEmail} über {_options.Host}:{_options.Port} fehlgeschlagen " +
                $"({ex.GetType().Name}).",
                ex);
        }
    }
}

/// <summary>
/// Raised when a message could not be handed to the mail provider. The inner
/// exception is kept for diagnostics but must not be rendered to end users,
/// because it can contain server details and credentials.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
