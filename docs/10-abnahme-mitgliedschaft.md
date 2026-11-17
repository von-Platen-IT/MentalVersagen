# 10 — Abnahmeliste Stripe-Mitgliedschaft

Stand: Refactoring „Membership-Konsolidierung“. Automatisierte Tests:
**271 erfolgreich, 0 Fehler**.

| ID | Szenario | Ergebnis |
|---|---|---|
| T01 | Funktion aus, Premium-Artikel | **Bestanden** (Unit-Test: Premium ist admin-only) |
| T02 | Funktion an, anonymer Besucher, Premium-Artikel innerhalb `TeaserDelayDays` | **Bestanden** (Unit-Test + Live-Teaser-Prüfung) |
| T03 | Funktion an, anonymer Besucher, Premium-Artikel nach `TeaserDelayDays` | **Bestanden** (Unit-Test) |
| T04 | Funktion an, angemeldeter Nicht-Mitglied, Premium-Artikel innerhalb `TeaserDelayDays` | **Bestanden** (Unit-Test) |
| T05 | Admin-Freigabe mit Ablaufdatum | **Bestanden** (Unit-Test) |
| T06 | Admin entzieht Freigabe, Audit-Log | **Bestanden** (Unit-Test) |
| T07 | Checkout-Session mit korrekter `ClientReferenceId`, Preis-ID und URLs | **Bestanden** (Controller-Test) |
| T08 | Webhook mit ungültiger Signatur | **Bestanden** (Controller-Test) |
| T09 | `checkout.session.completed` aktiviert Profil | **Bestanden** (Handler-Test + Dev-Webhook live) |
| T10 | Portal-Session mit `StripeCustomerId` | **Bestanden** (Controller-Test) |
| T11 | Login-Sync aktualisiert veraltetes Profil | **Bestanden** (Unit-Test) |
| T12 | Login-Sync-Fehler behält letzten Status | **Bestanden** (Unit-Test) |
| T13 | Hintergrundabgleich aktualisiert Abos | **Bestanden** (Unit-Test) |
| T14 | Hintergrundabgleich bei deaktivierter Funktion | **Bestanden** (Unit-Test) |
| T15 | Aktives Mitglied sieht Premium-Volltext | **Bestanden** (Unit-Test) |

## Zusätzliche Tests nach dem Refactoring

| Test | Ergebnis |
|---|---|
| `PlanId` wird aus dem Stripe-Snapshot gespeichert | **Bestanden** (Unit-Test) |
| Admin-Filter „Zahlung offen“ | **Bestanden** (Unit-Test) |
| Admin-Filter „Gekündigt“ | **Bestanden** (Unit-Test) |
| DonationService zeichnet anonyme Spenden auf | **Bestanden** (Unit-Test) |
| Keine Referenzen auf `Subscription`, `IStripeService` oder `PremiumAccess` im aktuellen Code | **Bestanden** (Suche) |

## Noch offen

| Punkt | Status | Voraussetzung |
|---|---|---|
| Vollständiger Stripe-Testmodus-Lauf mit Stripe CLI | **Offen** | Echte Testschlüssel, Preis-IDs und Webhook-Secret |
| Kündigung, fehlgeschlagene Zahlung, verpasster Webhook mit Testuhr | **Offen** | Stripe-Testkonto und Stripe CLI |
| Rechtliche Texte (AGB, Widerruf, Datenschutz) | **Offen** | Juristische Freigabe |
| Steuerliche Behandlung | **Offen** | Steuerberatung |

## Hinweise

- Die Live-Prüfung von T02/T09 erfolgte mit der Dev-Fake-Anbindung ohne echte
  Stripe-Schlüssel. Dabei wurde bestätigt: kein Volltext im HTML, Teaser und
  CTA vorhanden, Webhook HTTP 200, Profil `Active`, Idempotenzzeile angelegt.
- Die Testdaten wurden anschließend vollständig entfernt.
- Der alte Abo-Flow wurde entfernt; `MembershipProfile` ist die einzige
  Abo-Quelle.