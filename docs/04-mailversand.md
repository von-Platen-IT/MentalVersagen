# 04 — Mailversand (SMTP) — Umsetzungsvorgabe

Status: Vorgabe für den Coding-Agenten. Ersetzt den Dev-Platzhalter `DevEmailSender`
durch einen echten, konfigurierbaren SMTP-Versand. Ziel ist, dass der Doppel-Opt-in
(Registrierung **und** Newsletter) auf Produktion funktioniert.

---

## 1. Ausgangslage (verifiziert)

Der Fehler, der die Registrierung auf `https://mentalversagen.de/Account/Register`
abbrechen lässt:

```
System.UnauthorizedAccessException: Access to the path '/app/App_Data' is denied.
   at BlogCms.Infrastructure.Email.DevEmailSender.SendAsync(...)
   at BlogCms.Web.Pages.Account.RegisterModel.OnPostAsync(...)
```

Ursache ist eine Dreifach-Kette:

1. `src/BlogCms.Web/Program.cs:76` registriert **ausschließlich** den Dev-Platzhalter:
   ```csharp
   builder.Services.AddScoped<IAppEmailSender, DevEmailSender>();
   ```
   Es gibt im Repo **keinen** SMTP-Sender. Die Mails werden nie versendet, nur geschrieben.
2. `DevEmailSender` schreibt nach `Path.Combine(AppContext.BaseDirectory, "App_Data", "emails")`,
   also `/app/App_Data/emails`. Im Dockerfile ist nur `/app/wwwroot/uploads` per `chown`
   an den Nicht-Root-Benutzer `$APP_UID` übergeben (`Dockerfile:45`). `/app/App_Data`
   gehört `root`, der Container läuft aber als `USER $APP_UID` (`Dockerfile:48`).
   → `UnauthorizedAccessException`.
3. `Register.cshtml.cs:157` ruft den Sender **nach** dem Anlegen des Users auf, ohne
   Fehlerbehandlung. Die Exception verlässt den Handler, landet in der
   `ExceptionHandlerMiddleware` und erzeugt die gelbe Fehlerseite. Der User ist zu
   diesem Zeitpunkt **bereits in der DB** — die Registrierung ist faktisch erfolgreich,
   nur der Mailversand nicht.

Zweiter betroffener Pfad: `Pages/Newsletter/Subscribe.cshtml.cs:69` hat dieselbe
Kopplung und denselben Fehler.

**Konsequenz:** Jede Registrierung und jedes Newsletter-Abo erzeugt aktuell ein
halbfertiges Konto ohne Bestätigungsmail. Das ist ein Produktionsfehler, kein
Konfigurationsfehler.

### Bestehende Abstraktion (bleibt unverändert)

`src/BlogCms.Infrastructure/Email/IAppEmailSender.cs`:

```csharp
Task SendAsync(string toEmail, string subject, string htmlBody,
               CancellationToken cancellationToken = default);
```

Die Signatur wird **nicht** geändert. Sie wird an drei Stellen benutzt
(`Register.cshtml.cs`, `Subscribe.cshtml.cs`, `IAppEmailSender.cs` selbst) — eine
Signaturänderung erzwingt Änderungen an beiden PageModels und ist für dieses Vorhaben
nicht nötig. Wenn ein Absendername (`From`) gebraucht wird, kommt er **über die
Konfiguration**, nicht über die Signatur.

---

## 2. Anforderungen an die Lösung

| Nr. | Anforderung |
|---|---|
| A1 | Die Anwendung muss ohne E-Mail-Credentials starten können (Tests, CI, frischer Dev-Container). |
| A2 | Auf Produktion wird über SMTP versendet, konfiguriert per Konfiguration, ohne Codeänderung. |
| A3 | Ein Versandfehler darf **nie** die Registrierung oder das Abo abbrechen. Der User soll angelegt werden, das Abo soll bestehen, und der Nutzer sieht eine verständliche Meldung statt einer Fehlerseite. |
| A4 | Passwörter, SMTP-Credentials und API-Keys dürfen **nirgends** im Quellcode, in `appsettings.json` oder im Git-Repository stehen. |
| A5 | Der gewählte Provider muss austauschbar sein, ohne dass die Aufrufenden Code ändern. |
| A6 | `DevEmailSender` bleibt als Development-Fallback erhalten (dort ist das Schreiben nach `App_Data` unkritisch, weil der Dev-Root nutzt). |
| A7 | Das bestehende Options-Muster (`CommentOptions`, `MediaOptions`, `PaymentOptions`) wird eingehalten — inklusive `SectionName`-Konstante und sinnvoller Defaults. |
| A8 | Zugangsdaten und Fehlerdetails erscheinen **nicht** in Logs und **nicht** in der Fehlerseite. |

### Providerwahl

Vorgesehen ist **SMTP** als gemeinsamer Nenner. Bewusst **kein** HTTP-API-Client
(Brevo/Mailjet/Resend), weil dann pro Provider ein eigener Client dazukäme und der
Austausch nicht mehr über die Konfiguration liefe.

Für den IONOS-Produktionsserver (bereits im Einsatz, kein zusätzlicher Dienst nötig):

| Einstellung | Wert |
|---|---|
| Server | `smtp.ionos.com` (bzw. `smtp.ionos.de`) |
| Port | `587`, Verschlüsselung STARTTLS — **587 als Default**, da 465 in manchen Netzen blockiert ist |
| Benutzername | die vollständige Absenderadresse, z. B. `noreply@mentalversagen.de` |
| Passwort | das Postfach-Passwort (Sonderzeichen müssen funktionieren) |
| Absender | `noreply@mentalversagen.de` (SPF/DKIM im IONOS-Postfach hinterlegen) |

Ein SMTP-Paket wird gebraucht — `System.Net.Mail.SmtpClient` ist in .NET als obsolet
markiert und darf nicht verwendet werden. Empfohlen: **MailKit** (`MailKit` + `MimeKit`),
das STARTTLS, moderne Auth-Mechanismen und Zeichensatzkorrektheit abdeckt; die aktuelle
Version unterstützt .NET 10 ausdrücklich (MailKit 4.17.0 / MimeKit 4.17.0). Die
`PackageReference` kommt in `src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj`
(dort stehen die Microsoft-Pakete auf `10.0.12`, Ziel-Framework `net10.0`).

---

## 3. Vorgeschlagenes Design

Das Muster ist im Repo bereits etabliert: `PaymentOptions.IsConfigured` steuert, ob
`StripeService` oder `FakeStripeService` in der DI landet (`Program.cs:132-141`),
`MediaOptions.Provider` wählt zwischen `S3StorageService` und
`LocalDiskStorageService` (`Program.cs:122-128`). Der Mailversand folgt demselben Muster.

### 3.1 Neue Datei `EmailOptions`

Pfad: `src/BlogCms.Infrastructure/Email/EmailOptions.cs`

```csharp
namespace BlogCms.Infrastructure.Email;

/// <summary>
/// Konfiguration des Mailversands, gebunden aus dem Abschnitt "Email"
/// der Konfiguration (siehe docs/04-mailversand.md).
/// Ohne gesetzte Zugangsdaten bleibt der Development-Fallback aktiv.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Smtp" (Produktion) oder "Dev" (lokale Entwicklung, schreibt Dateien).</summary>
    public string Provider { get; set; } = "Dev";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Absenderadresse, z. B. noreply@mentalversagen.de.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Anzeigename des Absenders, z. B. "MentalVersagen".</summary>
    public string FromDisplayName { get; set; } = "MentalVersagen";

    /// <summary>
    /// True, wenn Provider-SMTP und vollständige Zugangsdaten gesetzt sind.
    /// Steuert die Auswahl zwischen SmtpEmailSender und DevEmailSender.
    /// </summary>
    public bool IsConfigured =>
        string.Equals(Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
```

`IsConfigured` als berechnete Eigenschaft — genau wie in `PaymentOptions`. Damit ist
A1 erfüllt: fehlen Zugangsdaten, greift automatisch der Dev-Fallback.

### 3.2 Neuer `SmtpEmailSender`

Pfad: `src/BlogCms.Infrastructure/Email/SmtpEmailSender.cs`

Anforderungen:

- Implementiert `IAppEmailSender`.
- Nimmt `IOptions<EmailOptions>` und `ILogger<SmtpEmailSender>` per Konstruktor.
- Baut pro Sendung eine `MimeMessage` mit Absender (`FromAddress` +
  `FromDisplayName`, auf ASCII umgebrochen, damit Sonderzeichen im Anzeigenamen
  nicht zu `MIME-Encoding-Problem` führen), `To`, `Subject` und HTML-TextBody.
- Verbindet mit **Timeout** (10 Sekunden), damit ein hängender SMTP-Server keinen
  Request blockiert.
- Bei `SmtpException` **und** bei Zeitüberschreitung: `LogError` mit Host/Port und
  **ohne** Passwort, Re-Ident als `MailKit.Net.Smtp.SmtpCommandFailedException`/
  `SmtpDeliveryException` — die konkrete Exception-Property kann das Passwort
  enthalten, deshalb darf sie nicht ungefiltert geloggt werden. Log-Ausgabe
  kontrollieren und begrenzen.
- `MailKit`-Clients sind threadsicher, aber Verbindungen nicht: Client per
  `SmtpClient`-Instanz wiederverwenden, pro `SendAsync` verbinden und trennen
  (`await client.DisconnectAsync`) — kein dauerhaft offenes Socket.
- `cancellationToken` durchreichen.

### 3.3 `DevEmailSender` härten

Auch im Dev-Modus darf ein fehlgeschlagener Mailversand keine 500er-Seite erzeugen:

- Das Zielverzeichnis in einen **konfigurierbaren** Pfad legen (Default
  weiterhin `App_Data/emails` unter `AppContext.BaseDirectory`), damit der
  Entwickler lokal einen anderen Ort angeben kann.
- Das Schreiben in `try`/`catch`; bei Fehlschlag `LogWarning`, **keine** Exception
  nach oben. Ein Dev-Code, der die App zum Absturz bringt, ist das, was das
  Deployment (unbemerkt) auf Produktion ausgeliefert hat.

### 3.4 Registrierung in `Program.cs`

Ersetze Zeile 76:

```csharp
// Alt, entfernen:
// builder.Services.AddScoped<IAppEmailSender, DevEmailSender>();

// Neu: Mailversand ist konfigurierbar. Ohne Zugangsdaten bleibt der
// Dev-Fallback aktiv, damit die App ohne Credentials startet.
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddScoped<IAppEmailSender>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
    return options.IsConfigured
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(serviceProvider)
        : ActivatorUtilities.CreateInstance<DevEmailSender>(serviceProvider);
});
```

Zusätzlich **beim Start loggen, welcher Provider aktiv ist** — aber nur das
Providername, nie Host mit Zugangsdaten:

```csharp
if (!app.Environment.IsDevelopment())
{
    // bestehende Produktions-Härtung
}
```

Sinngemäß an der bestehenden Stelle ergänzen:

```csharp
var mailOptions = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
app.Logger.LogInformation(
    "E-Mail-Versand aktiv: {Provider} (konfiguriert: {Configured})",
    mailOptions.Provider, mailOptions.IsConfigured);
```

**Ausdrücklich** loggen: Wenn `IsConfigured == false` und die Anwendung **nicht**
im Development-Modus läuft, ist das ein Konfigurationsfehler — die App läuft dann
auf Produktion ohne Versand. Das gehört als Warnung ins Log, damit es beim Deploy
auffällt.

### 3.5 Aufrufende Stellen entkoppeln (A3)

Das ist der eigentliche Kern des Bugs und darf nicht übersehen werden.

`src/BlogCms.Web/Pages/Account/Register.cshtml.cs:157`:

```csharp
// Alt: await _emailSender.SendAsync(user.Email!, "E-Mail-Adresse bestätigen", html);

// Neu: Ein Versandfehler darf die Registrierung nicht abbrechen. Der User ist
// zu diesem Zeitpunkt bereits angelegt und der Rolle Reader zugewiesen.
try
{
    await _emailSender.SendAsync(user.Email!, "E-Mail-Adresse bestätigen", html);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Bestätigungs-E-Mail an {Email} konnte nicht gesendet werden.",
        user.Email);
}

return RedirectToPage("/Account/RegisterConfirmation", new { email = Input.Email });
```

Der Konstruktor von `RegisterModel` (`Register.cshtml.cs:20-30`) hat aktuell **kein**
`ILogger`. Ein `ILogger<RegisterModel>` ist dort neu zu injizieren und zu verdrahten.

`src/BlogCms.Web/Pages/Newsletter/Subscribe.cshtml.cs:69`: identisch absichern. Der
`Message`-Text für den Nutzer muss dann von „Bitte bestätige den Link in der soeben
gesendeten E-Mail" unterscheiden — **nur** wenn der Versand fehlgeschlagen ist:

```csharp
try
{
    await _emailSender.SendAsync(Email, "Newsletter-Anmeldung bestätigen", html, cancellationToken);
    Message = "Fast fertig: Bitte bestätige den Link in der soeben gesendeten E-Mail.";
}
catch (Exception ex)
{
    _logger.LogError(ex, "Bestätigungs-E-Mail für das Newsletter-Abo konnte nicht gesendet werden.");
    Message = "Die Bestätigungs-E-Mail konnte gerade nicht versendet werden. Bitte später erneut versuchen.";
}

ModelState.Clear();
return Page();
```

In `Subscribe.cshtml.cs` ist dafür ebenfalls ein `ILogger<SubscribeModel>` zu
injizieren.

Zusätzlich überlegen (als eigene Aufgabe, nicht zwingend Teil dieses Auftrags):
Bestätigungslinks sollten per Rate-Limit geschützt und das Konto bei dauerhaftem
Versandfehler mit einem Admin-Hinweis versehen werden — sonst sammeln sich
unbestätigte Konten, die sich nie anmelden können.

### 3.6 Konfiguration

`src/BlogCms.Web/appsettings.json` — Abschnitt ergänzen, **ohne** Secrets:

```json
"Email": {
  "Provider": "Dev",
  "Port": 587,
  "UseStartTls": true,
  "FromAddress": "",
  "FromDisplayName": "MentalVersagen"
}
```

Kein echtes Passwort in `appsettings.json`, auch nicht in
`appsettings.Development.json`. `.gitignore` deckt `.env` bereits ab (Zeile 8) —
vor dem Commit trotzdem mit `git status --short` prüfen, dass keine `.env` im
Staging landet.

Umgebungsvariablen (doppelter Unterstrich, so auch von `EnvFileLoader` unterstützt,
siehe `src/BlogCms.Web/Configuration/EnvFileLoader.cs`):

```
Email__Provider=Smtp
Email__Host=smtp.ionos.com
Email__Port=587
Email__UseStartTls=true
Email__UserName=noreply@mentalversagen.de
Email__Password=<GEHEIM>
Email__FromAddress=noreply@mentalversagen.de
Email__FromDisplayName=MentalVersagen
```

Auf dem Produktionsserver (`/home/deploy/mentalversagen/.env`, dort landen bereits
`POSTGRES_*` und `ConnectionStrings__DefaultConnection`) die `Email__`-Zeilen
ergänzen. Die Datei steht **nicht** im Git-Repository — sie wird per `env_file`
eingebunden, und `.env` ist in `.gitignore` (Zeile 8).

---

## 4. Tests

Testprojekt: `tests/BlogCms.Tests/` (xUnit, `Microsoft.NET.Test.Sdk` 17.14.1,
`Microsoft.EntityFrameworkCore.InMemory`). Neue Datei
`tests/BlogCms.Tests/EmailTests.cs`, Stil wie `NewsletterTests.cs`.

Pflichtfälle:

1. `EmailOptions.IsConfigured` ist `false`, wenn `Password` fehlt.
2. `EmailOptions.IsConfigured` ist `false`, wenn `Provider` nicht `Smtp` ist.
3. `EmailOptions.IsConfigured` ist `true` bei vollständiger SMTP-Konfiguration.
4. `DevEmailSender` wirft bei nicht anlegbarem Zielverzeichnis **keine** Exception,
   sondern loggt eine Warnung. Der Test setzt das Verzeichnis auf einen Pfad, der
   nicht anlegbar ist (z. B. ein Pfad, dessen Eltern eine Datei ist), und prüft,
   dass `SendAsync` ohne `throw` zurückkehrt.
5. Ein `SmtpEmailSender`-Test mit **ungültigem** Host endet nach Timeout/SMTP-Fehler
   mit einer Exception statt mit einem Hänger. **Kein** Test, der versucht, sich von
   einer Testsuite aus in echtes SMTP einzuloggen — die Tests bleiben netzunabhängig.

`dotnet test -v q --nologo` muss 0 Fehler liefern, **bevor** committet wird.

---

## 5. Abnahmekriterien

- [ ] `dotnet test -v q --nologo` → 0 Fehler.
- [ ] `dotnet build` ohne neue Warnung (das Projekt setzt kein `TreatWarningsAsErrors`;
      Maßstab ist: keine neue Warnung ggü. `main`).
- [ ] Ohne `Email__Password` gesetzt startet die App und loggt
      `E-Mail-Versand aktiv: Dev (konfiguriert: False)`.
- [ ] `Program.cs` enthält **keine** fest verdrahtete `DevEmailSender`-Registrierung mehr.
- [ ] Kein Passwort, kein API-Key, kein SMTP-Credential in irgendeiner committeten Datei;
      `git grep -i password` zeigt nur das Options-Feld und die Connection-String-Variablen.
- [ ] Registrierung mit SMTP **nicht** erreichbar: Der Bestätigungslink landet im
      Postfach. Die Seite zeigt die Bestätigungs-View, keine Fehlerseite.
- [ ] Registrierung mit absichtlich kaputtem SMTP-Host: **kein** 500er, der User
      existiert, eine verständliche Meldung erscheint, das Log enthält eine
      Fehlermeldung **ohne** Zugangsdaten.
- [ ] Newsletter-Abo verhält sich identisch.
- [ ] In `appsettings.json` steht kein Secret.

---

## 6. Nach dem Merge (Deploy — übernimmt Bernd, nicht der Agent)

```bash
# 1) auf dem Produktionsserver
cd /home/deploy/mentalversagen
ALT=$(git rev-parse --short HEAD) && git pull
git diff $ALT..HEAD --stat -- 'docker-compose*' Dockerfile   # erwartet: leer

# 2) .env um die Email__-Zeilen ergänzen (Passwort einfügen)

# 3) Snapshot (Postgres, keine Migration erwartet)
docker exec mentalversagen-db pg_dump \
  -U $(docker exec mentalversagen-db printenv POSTGRES_USER) \
  -d $(docker exec mentalversagen-db printenv POSTGRES_DB) \
  > /home/deploy/backups/mv-pre-email-$(date +%Y%m%d-%H%M%S).sql

# 4) Bauen und live schalten — NUR die Prod-Datei
docker compose -f docker-compose.prod.yml up -d --build

# 5) Verifikation
docker inspect mentalversagen-app --format '{{.Created}}'
docker logs --tail 50 mentalversagen-app | grep -i 'E-Mail-Versand aktiv'
```

Erwarteter Log-Eintrag nach Schritt 5:
`E-Mail-Versand aktiv: Smtp (konfiguriert: True)`.

Bleibt dort `Dev (konfiguriert: False)` stehen, sind die `Email__`-Variablen nicht in
der `.env` angekommen — **kein** funktionierender Versand. Der Container läuft
sonst scheinbar gesund weiter, der Fehler fällt nur bei der nächsten Registrierung auf.

Ein `docker-compose.yml` (Development) mit Dev-Config darf dabei **nicht** mit
hochgeladen werden; auf MentalVersagen gilt ausschließlich
`docker compose -f docker-compose.prod.yml`.

---

## 7. Ausdrücklich nicht Teil dieses Auftrags

- E-Mail-Vorlagen aus Dateien laden, mehrsprachige Betreffzeilen, Anhangsversand.
- HTTP-API-Provider (Brevo, Mailjet, Resend). Falls später gewünscht: eigene
  `IAppEmailSender`-Implementierung plus `Provider`-Wert, die Aufrufenden bleiben unberührt.
- Passwort-Zurücksetzen / E-Mail-Änderung. Beides nutzt `IAppEmailSender` und funktioniert
  dann automatisch mit — aber die zugehörigen Pages existieren noch nicht.
- SPF/DKIM/DMARC im IONOS-Postfach einrichten. Organisatorisch, nicht Code.