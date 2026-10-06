namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Konfiguration des Mailversands, gebunden aus dem Abschnitt "Email" der
/// Konfiguration (siehe docs/04-mailversand.md). Zugangsdaten kommen
/// ausschließlich aus Umgebungsvariablen (Email__Host, Email__Password, …) —
/// niemals aus einer committeten Datei.
/// Ohne gesetzte Zugangsdaten bleibt der Development-Fallback aktiv, damit die
/// Anwendung ohne Credentials startet.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// "Smtp" (Produktion) oder "Dev" (lokale Entwicklung, schreibt Dateien).
    /// </summary>
    public string Provider { get; set; } = "Dev";

    /// <summary>SMTP-Server, z. B. smtp.ionos.com.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP-Port. 587 (STARTTLS) als Default, da 465 in manchen Netzen blockiert ist.</summary>
    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    /// <summary>Login für das Postfach, üblicherweise die vollständige Absenderadresse.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Postfach-Passwort. Nur als Geheimnis, nie in appsettings.json.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Absenderadresse, z. B. noreply@mentalversagen.de.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Anzeigename des Absenders, z. B. "MentalVersagen".</summary>
    public string FromDisplayName { get; set; } = "MentalVersagen";

    /// <summary>
    /// Zielverzeichnis des Development-Fallbacks. Leer = App_Data/emails unter
    /// dem Basisverzeichnis der Anwendung.
    /// </summary>
    public string DevOutputPath { get; set; } = string.Empty;

    /// <summary>
    /// True, wenn Provider "Smtp" ist und alle Zugangsdaten gesetzt sind.
    /// Steuert die Auswahl zwischen SmtpEmailSender und DevEmailSender.
    /// </summary>
    public bool IsConfigured =>
        string.Equals(Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
