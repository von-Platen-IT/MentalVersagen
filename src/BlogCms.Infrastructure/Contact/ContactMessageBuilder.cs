using System.Net;

namespace BlogCms.Infrastructure.Contact;

/// <summary>
/// Builds the subject and HTML body of a contact message. Kept separate from
/// the Razor Page so the sanitising rules are unit-testable.
/// </summary>
public static class ContactMessageBuilder
{
    /// <summary>
    /// Removes line breaks so user input cannot inject additional mail headers.
    /// </summary>
    public static string BuildSubject(string? subject)
    {
        var cleaned = (subject ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? "[Kontakt] Kontaktanfrage"
            : $"[Kontakt] {cleaned}";
    }

    /// <summary>
    /// Renders the message as HTML. Every user-supplied value is encoded, so
    /// the mail client never receives injected markup.
    /// </summary>
    public static string BuildBody(string name, string email, string? subject, string message)
    {
        var safeName = WebUtility.HtmlEncode(name.Trim());
        var safeEmail = WebUtility.HtmlEncode(email.Trim());
        var safeSubject = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(subject)
            ? "(kein Betreff)"
            : subject.Trim());
        var safeMessage = WebUtility.HtmlEncode(message.Trim())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);

        return $"""
            <h2>Neue Kontaktanfrage</h2>
            <p>
                <strong>Name:</strong> {safeName}<br />
                <strong>E-Mail:</strong> {safeEmail}<br />
                <strong>Betreff:</strong> {safeSubject}
            </p>
            <hr />
            <p>{safeMessage}</p>
            """;
    }
}
