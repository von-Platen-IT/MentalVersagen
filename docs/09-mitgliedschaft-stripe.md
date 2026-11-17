# 09 — Stripe-Mitgliedschaft

## Überblick

Die Mitgliedschaftsfunktion ist die **einzige Abo-Quelle**. Die frühere
`Subscription`-Tabelle und `User.SubscriptionStatus` wurden entfernt. Die
Funktion wird über `Membership:Enabled` aktiviert und ist standardmäßig **aus**.

Bei `false` gilt:

- `Public`-Artikel sind frei.
- `Registered`-Artikel benötigen eine Anmeldung.
- `Premium`-Artikel sind nur für Admins sichtbar.
- Die Teaser-Wartezeit wird nicht angewendet.

Zugriffsregeln bei aktivierter Funktion:

- `Public`-Artikel sind immer vollständig sichtbar.
- `Registered`-Artikel sind für angemeldete Benutzer sichtbar.
- `Premium`-Artikel sind für Mitglieder, Administratoren und freigeschaltete
  Benutzer sofort sichtbar; für alle anderen erst nach `TeaserDelayDays`.
- Die Kulanzzeit `GracePeriodDays` gilt nach dem Ende der Abrechnungsperiode.

## Konfiguration

Alle Werte liegen in der Sektion `Membership`. Echte Werte gehören in
User Secrets oder Umgebungsvariablen, **nicht** in `appsettings.json`.

| Einstellung | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `Enabled` | `Membership__Enabled` | `false` | Hauptschalter |
| `SecretKey` | `Membership__SecretKey` | leer | Stripe Secret Key |
| `PublishableKey` | `Membership__PublishableKey` | leer | Stripe Publishable Key |
| `WebhookSecret` | `Membership__WebhookSecret` | leer | Signing Secret des neuen Webhooks |
| `PriceIdMonthly` | `Membership__PriceIdMonthly` | leer | Stripe-Preis-ID Monatsabo |
| `PriceIdYearly` | `Membership__PriceIdYearly` | leer | Stripe-Preis-ID Jahresabo |
| `TeaserDelayDays` | `Membership__TeaserDelayDays` | `7` | Tage bis Premium-Artikel frei werden |
| `GracePeriodDays` | `Membership__GracePeriodDays` | `3` | Kulanzzeit nach Periodenende |
| `SyncOnLoginIfOlderThanHours` | `Membership__SyncOnLoginIfOlderThanHours` | `24` | Login-Sync-Schwelle |
| `SyncIntervalHours` | `Membership__SyncIntervalHours` | `24` | Intervall des Hintergrundjobs |
| `CheckoutSuccessPath` | `Membership__CheckoutSuccessPath` | `/Membership/CheckoutComplete` | Erfolgsseite |
| `CheckoutCancelPath` | `Membership__CheckoutCancelPath` | `/Unterstuetzer/Index` | Abbruchseite |
| `PortalReturnPath` | `Membership__PortalReturnPath` | `/Account/Manage` | Rückkehr aus dem Portal |

Beispiel für User Secrets:

```bash
dotnet user-secrets set "Membership:Enabled" "true" --project src/BlogCms.Web
dotnet user-secrets set "Membership:SecretKey" "sk_test_..." --project src/BlogCms.Web
dotnet user-secrets set "Membership:WebhookSecret" "whsec_..." --project src/BlogCms.Web
dotnet user-secrets set "Membership:PriceIdMonthly" "price_..." --project src/BlogCms.Web
dotnet user-secrets set "Membership:PriceIdYearly" "price_..." --project src/BlogCms.Web
```

## Stripe-Dashboard

1. Produkt und zwei Preise (monatlich/jährlich) anlegen.
2. Die Preis-IDs in `PriceIdMonthly` und `PriceIdYearly` eintragen.
3. Webhook-Endpunkt `https://<domain>/webhooks/stripe` anlegen.
4. Folgende Events abonnieren:
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.paid`
   - `invoice.payment_failed`
5. Das Signing Secret in `Membership:WebhookSecret` eintragen.
6. Das Kundenportal im Stripe-Dashboard aktivieren und konfigurieren.

## Ablauf

1. Angemeldeter Benutzer wählt auf `/Unterstuetzer` monatlich oder jährlich.
2. `POST /membership/checkout` erstellt eine Subscription-Checkout-Session mit
   `ClientReferenceId` und `Metadata["userId"]`.
3. Stripe leitet zur gehosteten Zahlungsseite weiter.
4. Die Erfolgsseite ändert **keinen** Status.
5. `POST /webhooks/stripe` verifiziert die Signatur, speichert das Event
   idempotent und aktualisiert `MembershipProfile`.
6. Die Artikelseite entscheidet serverseitig über den Volltext.
7. Login-Sync und Hintergrundjob halten den Status aktuell.

## Hintergrundjob

`MembershipSyncBackgroundService` läuft im Intervall `SyncIntervalHours`
(mindestens 1 Stunde) und arbeitet in Batches von 50 Profilen. Bei deaktivierter
Funktion werden keine Stripe-Aufrufe ausgeführt. Der Login-Sync aktualisiert ein
Profil nur, wenn `LastSyncedAt` älter als `SyncOnLoginIfOlderThanHours` ist.
Schlägt ein Abruf fehl, bleibt der letzte bekannte Status erhalten.

## Admin-Abo-Übersicht und Freigaben

Unter `/Admin/Memberships` sehen Administratoren eine Übersicht aller Abos:

- Zusammenfassung: Gesamt, Aktiv, Zahlung offen, Gekündigt, Freigeschaltet,
  Basis.
- Tabelle mit Tarif (`PlanId`), Laufzeit, Kündigung zum Periodenende und letztem
  Stripe-Sync.
- Suche nach Name/E-Mail.
- Filter: Alle, Aktiv, Zahlung offen, Gekündigt, Freigeschaltet, Basis.

Aktionen:

- Freigabe mit optionalem Ablaufdatum und Notiz setzen.
- Freigabe entziehen.
- Manuellen Stripe-Sync auslösen.

Jede Änderung wird in `MembershipAuditLog` protokolliert. Die Seite ist nur bei
`Membership:Enabled = true` und mit Admin-Rolle erreichbar.

## Fehlerbehebung

| Symptom | Ursache / Lösung |
|---|---|
| `404` auf Membership-Seiten | `Membership:Enabled` ist `false`; Flag aktivieren. |
| Webhook antwortet `400` | Signatur ungültig: `Membership:WebhookSecret` prüfen. |
| Status wird nicht aktualisiert | Keine Benutzerzuordnung: `ClientReferenceId` bzw. `Metadata["userId"]` prüfen. |
| Stripe nicht erreichbar | Letzter Status bleibt erhalten; Logs prüfen und Sync erneut auslösen. |
| Portal leitet nur zurück | Für den Benutzer ist keine `StripeCustomerId` gespeichert. |
| Keine Webhook-Events | Endpoint-URL und Event-Auswahl im Stripe-Dashboard prüfen. |

## Sicherheit

- Der Zugriff auf den Volltext wird ausschließlich serverseitig entschieden.
- Webhook-Signaturen werden mit `Stripe.EventUtility.ConstructEvent` geprüft.
- Stripe-Geheimnisse stehen nicht im Repository und werden nicht geloggt.
- Admin-Endpunkte sind serverseitig über `RequireAdmin` geschützt.
- Für Artikelseiten gibt es keinen access-unabhängigen Output-Cache.

## Offene Punkte

- Rechtliche Texte (AGB, Widerruf, Datenschutz) sind Platzhalter.
- Steuerliche Behandlung von Mitgliedschaften ist zu klären.
- Ein vollständiger Testlauf im Stripe-Testmodus mit echten Testschlüsseln,
  Stripe CLI und Testuhr steht noch aus.