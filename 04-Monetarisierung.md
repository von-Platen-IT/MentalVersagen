# 04 — Monetarisierung

Referenzierte Entitäten aus `DataSchema.md`: `Subscription`, `Donation`, `NewsletterSubscriber`, `User.SubscriptionStatus`.

## Teil A — Membership / Paywall

Fachliche Referenz: [`FeatureFix1.MD`](FeatureFix1.MD) (Regeln BR-110 bis BR-114).

### Zugriffsstufen (BR-110/113)
- Beiträge tragen eine `AccessLevel`-Stufe:
  - `Public` — für alle lesbar,
  - `Registered` — nur für angemeldete Benutzer,
  - `Premium` — nur mit aktiver Berechtigung (`SubscriptionStatus = Active`) oder als `Admin`.
- Ein Benutzerkonto allein berechtigt **nicht** automatisch zum Premium-Zugriff.

### Funktionale Anforderungen
- Nutzer können ein kostenpflichtiges Abo abschließen (monatlich/jährlich, Plan-Definition liegt in Stripe, nicht in der eigenen DB dupliziert — `Subscription.PlanId` referenziert nur die Stripe-Plan-ID).
- Checkout erfolgt über **Stripe Checkout** (gehostete Zahlungsseite) — reduziert PCI-Compliance-Aufwand, da keine Kartendaten die eigene Infrastruktur berühren.
- Nach erfolgreichem Checkout: Stripe sendet Webhook-Events, das Backend verarbeitet u. a.:
  - `checkout.session.completed` → `Subscription`-Eintrag anlegen
  - `invoice.paid` → `Subscription.Status = Active`, `CurrentPeriodEnd` aktualisieren
  - `customer.subscription.updated` → Statusänderungen übernehmen
  - `customer.subscription.deleted` → `Subscription.Status = Canceled`, `User.SubscriptionStatus` entsprechend zurücksetzen
- `User.SubscriptionStatus` ist ein **denormalisiertes Cache-Feld** für schnelle Berechtigungsprüfungen (z. B. Paywall-Check beim Artikel-Rendering) — Quelle der Wahrheit bleibt `Subscription`.
- Webhook-Endpunkt verifiziert die Stripe-Signatur (verpflichtend, verhindert gefälschte Zahlungsbestätigungen).

### Kündigung
- Nutzer kann Abo über ein Stripe Customer Portal (gehostet von Stripe) selbst verwalten/kündigen — kein eigenes Kündigungs-UI notwendig in v1.

## Teil B — Einmalige Spenden

- Zusätzlich zum Abo: einmalige Spende über Stripe Checkout (Einzelzahlung statt Abo) oder PayPal-Button als Alternative.
- Erfolgreiche Zahlungen erzeugen einen `Donation`-Eintrag, unabhängig davon, ob der Spender registrierter `User` ist.
- Anonyme Spenden sind möglich (`Donation.UserId = null`).

## Teil C — Newsletter

- Eintragung über Formular (Startseite, Artikelseiten, dedizierte Landingpage).
- **Double-Opt-in verpflichtend**: nach Eintragung wird eine Bestätigungs-E-Mail versendet, `ConfirmedAt` wird erst nach Klick auf den Bestätigungslink gesetzt. Unbestätigte Einträge werden nach konfigurierbarer Frist automatisch gelöscht.
- Abmeldung jederzeit über Link in jeder versendeten Mail (`UnsubscribedAt` wird gesetzt, kein Hard-Delete, um erneute Anmeldung sauber zu behandeln).
- Versand in v1 über Anbindung an einen Transaktions-/Newsletter-Mailanbieter per API (kein eigener Massenversand-Server), um Zustellbarkeitsprobleme (Spam-Reputation) zu vermeiden.

## Akzeptanzkriterien

- [ ] Ein Stripe-Webhook mit ungültiger Signatur wird abgelehnt und nicht verarbeitet.
- [ ] Nach Kündigung im Stripe Customer Portal verliert der Nutzer spätestens nach Verarbeitung des `customer.subscription.deleted`-Events den Premium-Zugriff.
- [ ] Ein `Registered`-Beitrag ist nur für angemeldete Benutzer lesbar; für Anonyme erscheint der Teaser mit Login-CTA.
- [ ] Ein `Premium`-Beitrag ist nur mit aktiver Berechtigung lesbar; für Nicht-Berechtigte erscheint der Teaser mit CTA zur Mitgliedschaft.
- [ ] Eine Spende ohne Login ist möglich und wird korrekt als `Donation` mit `UserId = null` gespeichert.
- [ ] Newsletter-Eintrag ohne Bestätigung erhält keine weiteren Mails und wird nach Ablauf der Frist entfernt.
- [ ] Abmeldelink funktioniert ohne Login (über eindeutigen Token, nicht nur E-Mail-Adresse).
