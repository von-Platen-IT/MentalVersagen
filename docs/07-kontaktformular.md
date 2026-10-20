# 07 — Kontaktformular

Die Seite `/Kontakt` bietet Besuchern ein Formular, das eine E-Mail an den
Betreiber sendet. Die Zieladresse wird auf der Seite genannt und ist
konfigurierbar.

## Aufbau

| Datei | Zweck |
|---|---|
| `src/BlogCms.Web/Pages/Kontakt.cshtml` | Formular, Adressangabe, Captcha, Honeypot |
| `src/BlogCms.Web/Pages/Kontakt.cshtml.cs` | Validierung, Drosselung, Mailversand |
| `src/BlogCms.Infrastructure/Contact/ContactOptions.cs` | Zieladresse und Drosselungsgrenzen |
| `src/BlogCms.Infrastructure/Contact/ContactMessageBuilder.cs` | Betreff- und HTML-Aufbereitung |
| `src/BlogCms.Web/Security/ContactThrottle.cs` | IP-basierte Drosselung |

## Konfiguration

```json
"Contact": {
  "ToAddress": "bernd@mentalversagen.de",
  "MaxPerWindow": 3,
  "WindowMinutes": 10
}
```

- `ToAddress` — Empfänger der Kontaktanfragen. Leer oder nicht gesetzt fällt auf
  `bernd@mentalversagen.de` zurück.
- `MaxPerWindow` — maximale Sendeversuche pro IP im Zeitfenster.
- `WindowMinutes` — Länge des gleitenden Zeitfensters.

Die Werte können wie alle Optionen über Umgebungsvariablen überschrieben werden,
z. B. `Contact__ToAddress=...`.

## Spam-Schutz

1. **Captcha** — arithmetische Aufgabe mit signiertem Token und Mindestzeit
   (Time-Trap). Die Prüfung erfolgt ausschließlich serverseitig.
2. **Honeypot** — das Feld `ExtraField` ist für Menschen unsichtbar. Ist es
   gefüllt, wird die Nachricht verworfen, dem Bot aber ein Erfolg vorgetäuscht.
3. **Drosselung** — pro IP sind nur `MaxPerWindow` Versuche im Zeitfenster
   erlaubt. Danach erscheint eine verständliche Fehlermeldung.
4. **Längenlimits** — Name 2–100, E-Mail max. 200, Betreff max. 150,
   Nachricht 10–5000 Zeichen.
5. **Kein HTML aus Nutzereingaben** — alle Werte werden HTML-encodiert.
6. **Header-Injection-Schutz** — Zeilenumbrüche im Betreff werden entfernt.

## Mailversand

- Betreff: `[Kontakt] <Betreff>` bzw. `[Kontakt] Kontaktanfrage`.
- `Reply-To` ist die E-Mail-Adresse des Besuchers, damit direkt geantwortet
  werden kann. Die technische Absenderadresse bleibt die konfigurierte
  `Email:FromAddress`.
- Ein Versandfehler wird geloggt, aber nicht mit internen Details an den
  Besucher weitergegeben. Der Besucher erhält eine neutrale Fehlermeldung.

## Datenschutz

Die Datenschutzerklärung enthält im Abschnitt „4. Datenerfassung auf dieser
Website" einen Unterabschnitt „Kontaktformular" mit Zweck, Rechtsgrundlage und
Speicherdauer.

## Tests

`tests/BlogCms.Tests/ContactTests.cs` prüft:

- Defaults und SectionName der `ContactOptions`.
- Drosselung pro IP und getrennte Buckets.
- Pflichtfelder, E-Mail-Format, Mindestlängen und Datenschutz-Checkbox.
- Betreff-Bereinigung und HTML-Encodierung.
