namespace BlogCms.Infrastructure.Contact;

/// <summary>
/// Konfiguration des Kontaktformulars, gebunden aus dem Abschnitt "Contact"
/// der Konfiguration. Die Zieladresse ist bewusst konfigurierbar, damit sie
/// ohne Codeänderung angepasst werden kann.
/// </summary>
public sealed class ContactOptions
{
    public const string SectionName = "Contact";

    /// <summary>Empfänger der Kontaktanfragen.</summary>
    public string ToAddress { get; set; } = "bernd@mentalversagen.de";

    /// <summary>Maximale Anzahl Nachrichten pro Client im Zeitfenster.</summary>
    public int MaxPerWindow { get; set; } = 3;

    /// <summary>Länge des Drosselungs-Zeitfensters in Minuten.</summary>
    public int WindowMinutes { get; set; } = 10;
}
