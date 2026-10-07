> **Commit:** noch nicht eingecheckt — Stand: HEAD `f03bf60904b516fc6ebeddbce12d234d02fe85eb` (2026-10-06)

# Fix: Sicherheitsabfrage (Captcha) nach fehlgeschlagener Registrierung defekt

## Fehlerbild

Nach einem fehlgeschlagenen Registrierungsversuch (z. B. Verletzung der
Passwortregeln) schlägt die Sicherheitsabfrage dauerhaft fehl — auch bei
richtig beantworteter neuer Aufgabe erscheint
„Die Antwort auf die Sicherheitsabfrage ist nicht korrekt."

## Ursachenanalyse

Ablauf beim fehlgeschlagenen POST in
`src/BlogCms.Web/Pages/Account/Register.cshtml.cs`:

1. `OnPostAsync` erkennt den Identity-Fehler (Passwortregeln) via
   `AddErrors(result)`, ruft `IssueCaptcha()` auf und rendert die Seite neu.
2. `IssueCaptcha()` erzeugt eine **neue** Aufgabe + **neues** Token und setzt
   die Properties `CaptchaQuestion` und `CaptchaToken`.
3. Beim Re-Render zeigt `@Model.CaptchaQuestion` die **neue** Frage
   (Property ist nicht `[BindProperty]`, taucht also nicht im `ModelState` auf).
4. **Bug:** `<input asp-for="CaptchaToken" type="hidden" />` in
   `src/BlogCms.Web/Pages/Account/Register.cshtml` rendert den Wert
   **zuerst aus dem `ModelState`** — und dort steht noch der **alte** Token
   vom fehlgeschlagenen POST, denn `CaptchaToken` ist `[BindProperty]` und
   wurde beim POST gebunden.
5. Ergebnis: Der Nutzer sieht die neue Frage, das Formular sendet aber den
   alten Token mit der alten erwarteten Antwort → `CaptchaResult.WrongAnswer`
   → Fehlermeldung, obwohl die Antwort richtig war.

Der Fehler tritt bei **jedem** Re-Render nach POST auf: falsches Passwort,
Honeypot-Fehlschlag, Throttle, ungültige Captcha-Antwort.

## Korrekturplan

### Schritt 1 — Fix in `Register.cshtml.cs` (Kernfix)

In `IssueCaptcha()` nach dem Setzen der neuen Werte die veralteten
`ModelState`-Einträge entfernen, damit die Tag-Helper die neuen Modellwerte
rendern:

```csharp
private void IssueCaptcha()
{
    var challenge = _captcha.Issue();
    CaptchaQuestion = challenge.Question;
    CaptchaToken = challenge.Token;

    // Wichtig: alte POST-Werte aus dem ModelState entfernen, sonst rendert
    // <input asp-for="CaptchaToken" /> den ALTEN Token und die neue Frage
    // passt nicht mehr zum geprüften Token.
    ModelState.Remove(nameof(CaptchaToken));
    ModelState.Remove(nameof(CaptchaAnswer));
}
```

Nebeneffekt von `ModelState.Remove(nameof(CaptchaAnswer))`: Das Antwortfeld
wird nach einem Fehlversuch geleert — gewünschtes, sauberes Verhalten bei
neuer Aufgabe.

### Schritt 2 — Verifikation

- `dotnet build` bzw. Testlauf `tests/BlogCms.Tests` (CaptchaTests) prüfen.
- Manueller Smoke-Test des Ablaufs:
  1. `/Account/Register` aufrufen → Aufgabe lösen → Passwortregeln verletzen
     → Fehlermeldung erscheint, **neue** Aufgabe wird angezeigt.
  2. Neue Aufgabe korrekt beantworten + gültiges Passwort → Registrierung
     erfolgreich (Weiterleitung zu RegisterConfirmation).
  3. „Neue Aufgabe"-Button (AJAX) weiterhin funktionsfähig
     (`site.js` aktualisiert Frage + Token-Feld clientseitig — davon nicht
     betroffen).

## Betroffene Dateien

| Datei | Änderung |
| --- | --- |
| `src/BlogCms.Web/Pages/Account/Register.cshtml.cs` | `ModelState.Remove(...)` in `IssueCaptcha()` |

Keine weiteren Stellen betroffen: Die Newsletter-Subscribe-Seite nutzt kein
Captcha, und der AJAX-Refresh in `site.js` setzt Frage und Token bereits
korrekt clientseitig.