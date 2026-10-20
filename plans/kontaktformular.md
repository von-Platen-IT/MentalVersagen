# Kontaktformular mit Spam-Schutz

## Ziel

Zusätzliche Seite `/Kontakt` mit einem Formular, das eine E-Mail an
`bernd@mentalversagen.de` sendet. Die Adresse wird auf der Seite genannt, aber
durch mehrere Schutzschichten vor Spam bewahrt.

## Komponenten

### 1. ContactOptions

Neue Datei `src/BlogCms.Infrastructure/Contact/ContactOptions.cs`.

```csharp
public sealed class ContactOptions
{
    public const string SectionName = "Contact";

    /// <summary>Empfänger der Kontaktanfragen.</summary>
    public string ToAddress { get; set; } = "bernd@mentalversagen.de";

    /// <summary>Maximale Anzahl Nachrichten pro IP im Zeitfenster.</summary>
    public int MaxPerWindow { get; set; } = 3;

    /// <summary>Länge des Zeitfensters in Minuten.</summary>
    public int WindowMinutes { get; set; } = 10;
}
```

### 2. Reply-To im Mailversand

`IAppEmailSender.SendAsync` erhält einen optionalen Parameter:

```csharp
Task SendAsync(
    string toEmail,
    string subject,
    string htmlBody,
    string? replyTo = null,
    CancellationToken cancellationToken = default);
```

- `SmtpEmailSender`: bei gesetztem `replyTo` `message.ReplyTo.Add(MailboxAddress.Parse(replyTo))`.
- `DevEmailSender`: Reply-To als HTML-Kommentar in die Dev-Datei schreiben.
- Bestehende Aufrufe bleiben durch den Default kompatibel.

### 3. ContactThrottle

Neue Datei `src/BlogCms.Web/Security/ContactThrottle.cs` nach Vorbild von
`MemoryCacheRegistrationThrottle`.

- Interface `IContactThrottle` mit `bool TryAcquire(string? clientKey)`.
- Implementierung `MemoryCacheContactThrottle` mit `IMemoryCache`.
- Grenzen aus `ContactOptions` (`MaxPerWindow`, `WindowMinutes`).
- Cache-Key `contact-throttle:{ip}`.
- Registrierung in `Program.cs` als Singleton.

### 4. Kontakt-PageModel

Neue Datei `src/BlogCms.Web/Pages/Kontakt.cshtml.cs`.

Felder im `InputModel`:

| Feld | Regeln |
|---|---|
| `Name` | Pflicht, 2–100 Zeichen |
| `Email` | Pflicht, `EmailAddress`, max. 200 Zeichen |
| `Subject` | optional, max. 150 Zeichen |
| `Message` | Pflicht, 10–5000 Zeichen |
| `PrivacyAccepted` | Pflicht, muss `true` sein |

Zusätzlich:

- `CaptchaAnswer` / `CaptchaToken` (wie Registrierung).
- `ExtraField` als Honeypot.
- `OnGet` stellt ein Captcha aus.
- `OnGetCaptcha` liefert per JSON eine neue Aufgabe.
- `OnPostAsync` prüft in dieser Reihenfolge:
  1. Throttle pro IP.
  2. Honeypot (still verwerfen, neutrale Meldung).
  3. Captcha.
  4. `ModelState`.
  5. Mailversand.
- Bei Fehlern wird ein neues Captcha ausgestellt und `ModelState` für
  `CaptchaToken`/`CaptchaAnswer` bereinigt (gleiches Muster wie
  `RegisterModel.IssueCaptcha`).
- Betreff der Mail: `[Kontakt] {bereinigter Betreff}`; Zeilenumbrüche werden
  entfernt, um Header-Injection zu verhindern.
- Mail-Body: Name, E-Mail, Betreff und Nachricht HTML-encodiert.
- `Reply-To` = E-Mail-Adresse des Absenders.
- Versandfehler werden geloggt, der Nutzer erhält eine verständliche Meldung.
- Nach Erfolg: `ModelState.Clear()` und Erfolgsmeldung anzeigen.

### 5. Kontakt.cshtml

Neue Datei `src/BlogCms.Web/Pages/Kontakt.cshtml`.

- `_PageHeader` mit Titel „Kontakt".
- Hinweis auf die Adresse `bernd@mentalversagen.de` als `mailto:`-Link.
- Formular mit den Feldern aus dem PageModel.
- Captcha-Markup wie in `Register.cshtml`, Endpoint
  `@Url.Page("/Kontakt", "Captcha")`.
- Honeypot mit `.mv-honeypot`.
- Datenschutz-Checkbox mit Link auf `/Datenschutz`.
- `_ValidationScriptsPartial` im `Scripts`-Abschnitt.

### 6. Navigation

In `_Layout.cshtml` einen Footer-Link „Kontakt" zwischen Datenschutz und RSS
ergänzen. Optional zusätzlich in der Hauptnavigation.

### 7. Datenschutzerklärung

In `Datenschutz.cshtml` im Abschnitt „4. Datenerfassung auf dieser Website"
einen Unterabschnitt „Kontaktformular" ergänzen:

- Verarbeitete Daten: Name, E-Mail, Betreff, Nachricht.
- Zweck: Bearbeitung der Anfrage.
- Rechtsgrundlage: Art. 6 Abs. 1 lit. b bzw. lit. f DSGVO.
- Speicherdauer: bis zur Erledigung, sofern keine gesetzlichen Pflichten
  entgegenstehen.
- Hinweis, dass die Angabe der E-Mail-Adresse für die Beantwortung erforderlich
  ist.

### 8. Konfiguration

`appsettings.json`:

```json
"Contact": {
  "ToAddress": "bernd@mentalversagen.de",
  "MaxPerWindow": 3,
  "WindowMinutes": 10
}
```

### 9. Tests

Neue Datei `tests/BlogCms.Tests/ContactTests.cs`:

- `ContactOptions` Defaults und SectionName.
- `MemoryCacheContactThrottle`: erlaubt `MaxPerWindow` Versuche, blockt danach,
  trennt verschiedene IPs.
- Validierung des `InputModel` (Pflichtfelder, Längen, E-Mail-Format,
  Datenschutz-Checkbox).
- Betreff-Bereinigung gegen Zeilenumbrüche.

### 10. Dokumentation

Neue Datei `docs/07-kontaktformular.md` mit:

- Zweck und Route.
- Konfiguration (`Contact:ToAddress`, Rate-Limit).
- Spam-Schutzmaßnahmen.
- Verhalten bei Mailversandfehlern.
- Verweis auf Datenschutzabschnitt.

Verlinkung in `docs/README.md` ergänzen.

## Abnahmekriterien

- `/Kontakt` ist ohne Anmeldung erreichbar.
- Gültige Anfrage erzeugt eine Mail an `Contact:ToAddress` mit Reply-To.
- Ohne Captcha, mit gefülltem Honeypot oder bei überschrittenem Rate-Limit
  wird keine Mail versendet.
- Fehler werden verständlich angezeigt, ohne interne Details preiszugeben.
- Bestehende Tests bleiben grün.
