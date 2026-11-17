# 04 — Monetarisierung

Referenzierte Entitäten aus `DataSchema.md`: `MembershipProfile`, `StripeEvent`,
`MembershipAuditLog`, `Donation`, `NewsletterSubscriber`.

## Teil A — Membership / Paywall

Fachliche Referenz: [`FeatureFix1.MD`](FeatureFix1.MD) (Regeln BR-110 bis BR-114).

### Zugriffsstufen (BR-110/113)

Beiträge tragen eine `AccessLevel`-Stufe:

- `Public` — für alle lesbar.
- `Registered` — nur für angemeldete Benutzer.
- `Premium` — nur mit aktiver Mitgliedschaft, Admin-Freigabe oder als Admin.
  Nach `Membership:TeaserDelayDays` wird der Volltext für alle frei.

Die neue Membership-Funktion ist über `Membership:Enabled` steuerbar und
standardmäßig **aus**. Bei `false` gilt:

- `Public` ist frei.
- `Registered` benötigt eine Anmeldung.
- `Premium` ist nur für Admins sichtbar.

Das entspricht dem praktischen Verhalten vor der Einführung, da es keine
Abonnenten gab.

### Einzige Abo-Quelle

`MembershipProfile` ist die einzige persistente Abo-Tabelle. Die frühere
`Subscription`-Tabelle und `User.SubscriptionStatus` wurden entfernt.

Wichtige Felder:

- `StripeCustomerId`, `StripeSubscriptionId`
- `PlanId` (Stripe-Preis-ID für Monats-/Jahresabo)
- `Status`, `CurrentPeriodEnd`, `CancelAtPeriodEnd`
- `IsComplimentary`, `ComplimentaryUntil`
- `LastSyncedAt`

### Funktionale Anforderungen

- Nutzer können ein kostenpflichtiges Abo abschließen (monatlich/jährlich).
  Die Plan-Definition liegt in Stripe; `PlanId` referenziert nur die Preis-ID.
- Checkout erfolgt über **Stripe Checkout** (gehostete Zahlungsseite). Keine
  Kartendaten berühren den Blog-Server.
- Nach erfolgreichem Checkout sendet Stripe Webhook-Events. Der Blog verarbeitet:
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `invoice.paid`
  - `invoice.payment_failed`
- Webhook-Signaturen werden mit `Stripe.EventUtility.ConstructEvent` geprüft.
- Verarbeitete Events werden über `StripeEvent.StripeEventId` idempotent
  gespeichert.
- Die Erfolgsseite nach dem Checkout ändert **keinen** Status. Nur der signierte
  Webhook ist Quelle der Wahrheit.
- Kündigung und Zahlungsmittelverwaltung erfolgen über das Stripe-Kundenportal.
- Ein Login-Sync und ein Hintergrundjob gleichen den Status regelmäßig mit
  Stripe ab. Schlägt ein Abruf fehl, bleibt der letzte bekannte Status erhalten.
- Admins können Benutzer ohne Zahlung freischalten (`IsComplimentary`) und
  Freigaben wieder entziehen. Jede Änderung wird in `MembershipAuditLog`
  protokolliert.
- Der Admin-Bereich `/Admin/Memberships` zeigt eine Übersicht über alle Abos:
  Status, Tarif (`PlanId`), Laufzeit, Kündigung zum Periodenende und letzten
  Sync.

### Kulanz und Wartezeit

- `Membership:GracePeriodDays` gilt nach dem Ende der Abrechnungsperiode für
  aktive, gekündigte und zahlungsverzögerte Abos.
- `Membership:TeaserDelayDays` bestimmt, wann ein Premium-Artikel für alle frei
  wird.

## Teil B — Einmalige Spenden

- Einmalige Spenden bleiben unabhängig von der Mitgliedschaft.
- Die Persistenz erfolgt über `Donation` und `DonationService`.
- Anonyme Spenden sind möglich (`Donation.UserId = null`).
- Die Oberfläche ist aktuell ein Platzhalter; die Backend-Aufzeichnung bleibt
  verfügbar.

## Teil C — Newsletter

- Eintragung über Formular (Startseite, Artikelseiten, dedizierte Landingpage).
- **Double-Opt-in verpflichtend**: nach Eintragung wird eine Bestätigungs-E-Mail
  versendet, `ConfirmedAt` wird erst nach Klick auf den Bestätigungslink gesetzt.
  Unbestätigte Einträge werden nach konfigurierbarer Frist automatisch gelöscht.
- Abmeldung jederzeit über Link in jeder versendeten Mail (`UnsubscribedAt` wird
  gesetzt, kein Hard-Delete, um erneute Anmeldung sauber zu behandeln).
- Versand in v1 über Anbindung an einen Transaktions-/Newsletter-Mailanbieter per
  API (kein eigener Massenversand-Server), um Zustellbarkeitsprobleme (Spam-
  Reputation) zu vermeiden.

## Akzeptanzkriterien

- [ ] Ein Stripe-Webhook mit ungültiger Signatur wird abgelehnt und nicht
      verarbeitet.
- [ ] Nach Kündigung im Stripe Customer Portal verliert der Nutzer spätestens
      nach Ablauf der Kulanzzeit den Premium-Zugriff.
- [ ] Ein `Registered`-Beitrag ist nur für angemeldete Benutzer lesbar; für
      Anonyme erscheint der Teaser mit Login-CTA.
- [ ] Ein `Premium`-Beitrag ist nur mit aktiver Mitgliedschaft, Admin-Freigabe
      oder als Admin lesbar; für Nicht-Berechtigte erscheint der Teaser mit CTA
      zur Mitgliedschaft.
- [ ] Nach `TeaserDelayDays` wird der Premium-Volltext für alle frei.
- [ ] Eine Spende ohne Login ist möglich und wird korrekt als `Donation` mit
      `UserId = null` gespeichert.
- [ ] Newsletter-Eintrag ohne Bestätigung erhält keine weiteren Mails und wird
      nach Ablauf der Frist entfernt.
- [ ] Abmeldelink funktioniert ohne Login (über eindeutigen Token, nicht nur
      E-Mail-Adresse).
