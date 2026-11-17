# Pflichtenheft — Stripe-Mitgliedschaft im ASP.NET-Blog

Dieses Pflichtenheft definiert die verbindlichen Anforderungen für die neue
Stripe-Mitgliedschaft. Es ergänzt die bestehenden Dokumente
[`04-Monetarisierung.md`](04-Monetarisierung.md) und [`DataSchema.md`](DataSchema.md).
Bei Widersprüchen gilt dieses Dokument für die neue Membership-Funktion.

## 1. Ziel und Geltungsbereich

Ziel ist eine zeit- und mitgliedschaftsbasierte Paywall:

- Nicht zahlende Besucher sehen bei Premium-Artikeln zunächst nur Teaser.
- Nach einer konfigurierbaren Wartezeit werden Premium-Artikel für alle lesbar.
- Mitglieder und Administratoren sehen Premium-Artikel sofort.
- Administratoren können Mitgliedschaften ohne Zahlung freischalten.
- Stripe Checkout, Webhooks, Kundenportal und ein täglicher Abgleich halten den
  Mitgliedsstatus aktuell.

**Wichtig:** `MembershipProfile` ist die einzige Abo-Quelle. Der frühere
`Subscription`-Workflow wurde entfernt. Die Funktion wird ausschließlich über
`Membership:Enabled` aktiviert.

## 2. Grundregeln

1. **Standard bleibt aus.** `Membership:Enabled` ist standardmäßig `false`.
   Bei `false` darf sich das Verhalten des Blogs für Besucher nicht ändern.
2. **Keine Geheimnisse ins Repository.** Stripe-Schlüssel, Webhook-Secrets und
   Preis-IDs gehören in User Secrets bzw. Umgebungsvariablen. In
   `appsettings.json` stehen nur leere Platzhalter.
3. **Serverseitige Zugriffsentscheidung.** Der Volltext wird nur dann in das
   Modell und in die Antwort aufgenommen, wenn der Server den Zugriff erlaubt.
   Kein Verstecken per CSS oder JavaScript.
4. **Nichts raten.** Bei Unklarheiten im bestehenden Code wird zuerst gelesen und
   gefragt, bevor Annahmen getroffen werden.
5. **Phasenweise Umsetzung.** Jede Phase endet mit lauffähigem Build und grünen
   Tests.
6. **Kleine Änderungen.** Keine unrelated Refactorings.

## 3. Konfiguration

Options-Klasse `MembershipOptions`, gebunden an die Sektion `Membership`:

| Feld | Typ | Standard | Bedeutung |
|---|---|---|---|
| `Enabled` | bool | `false` | Hauptschalter der neuen Funktion |
| `SecretKey` | string? | `null` | Stripe Secret Key (User Secrets) |
| `PublishableKey` | string? | `null` | Stripe Publishable Key |
| `WebhookSecret` | string? | `null` | Signing Secret des neuen Webhook-Endpunkts |
| `PriceIdMonthly` | string | `""` | Stripe-Preis-ID für das Monatsabo |
| `PriceIdYearly` | string | `""` | Stripe-Preis-ID für das Jahresabo |
| `TeaserDelayDays` | int | `7` | Tage nach `PublishedAt`, nach denen ein Premium-Artikel frei wird |
| `GracePeriodDays` | int | `3` | Kulanzzeit nach `CurrentPeriodEnd` |
| `SyncOnLoginIfOlderThanHours` | int | `24` | Login-Sync-Schwelle |
| `SyncIntervalHours` | int | `24` | Intervall des Hintergrundabgleichs |
| `CheckoutSuccessPath` | string | `/Membership/CheckoutComplete` | Erfolgsseite nach Checkout |
| `CheckoutCancelPath` | string | `/Unterstuetzer/Index` | Abbruchseite |
| `PortalReturnPath` | string | `/Account/Manage` | Rückkehr aus dem Kundenportal |

`appsettings.json` erhält den Abschnitt `Membership` mit `"Enabled": false` und
den obigen Standardwerten. Echte Werte werden nicht eingecheckt.

## 4. Datenmodell

### 4.1 `MembershipProfile`

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | Guid | PK |
| `UserId` | Guid | FK auf `User`, eindeutig |
| `StripeCustomerId` | string? | Stripe-Kunden-ID |
| `StripeSubscriptionId` | string? | Stripe-Abo-ID |
| `PlanId` | string? | Stripe-Preis-ID des aktuellen Tarifs |
| `Status` | `MembershipStatus` | `None`, `Incomplete`, `Trialing`, `Active`, `PastDue`, `Canceled`, `Unpaid` |
| `CurrentPeriodEnd` | DateTime? | Ende der aktuellen Abrechnungsperiode (UTC) |
| `CancelAtPeriodEnd` | bool | Kündigung zum Periodenende |
| `IsComplimentary` | bool | Admin-Freigabe ohne Zahlung |
| `ComplimentaryUntil` | DateTime? | Ablauf der Freigabe; `null` = unbegrenzt |
| `LastSyncedAt` | DateTime? | letzter erfolgreicher Stripe-Abgleich |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime | |

### 4.2 `StripeEvent`

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | Guid | PK |
| `StripeEventId` | string | eindeutige Stripe-Event-ID |
| `Type` | string | z. B. `checkout.session.completed` |
| `ProcessedAt` | DateTime | Verarbeitungszeitpunkt |
| `PayloadHash` | string? | SHA-256 des Rohtexts, kein Volltext |
| `CreatedAt` | DateTime | |

### 4.3 `MembershipAuditLog`

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | Guid | PK |
| `TargetUserId` | Guid | betroffener Benutzer |
| `ActorUserId` | Guid? | ausführender Admin, `null` bei Systemaktionen |
| `Action` | string | `Grant`, `Revoke`, `Sync`, `Webhook`, `Checkout` |
| `Details` | string? | kurze Beschreibung, keine Geheimnisse |
| `CreatedAt` | DateTime | |

### 4.4 Artikel-Felder

Die bestehende [`Article`](src/BlogCms.Domain/Entities/Article.cs:9)-Entität
enthält bereits alle benötigten Felder. Es werden **keine Duplikate** angelegt:

| Pflichtenheft-Begriff | Vorhandenes Feld |
|---|---|
| `PublishedAt` | `Article.PublishedAt` |
| `Summary` | `Article.Excerpt` |
| `MainImageUrl` | `Article.TitleImageUrl` |

Eine Migration ist für diese Felder nicht erforderlich.

## 5. Zugriffslogik

### 5.1 `HasFullAccess(user, now)`

```
if not MembershipOptions.Enabled:
    return true

if user is null:
    return false

if user.Role == Admin:
    return true

profile = MembershipProfile for user.Id
if profile is null:
    return false

if profile.IsComplimentary:
    return profile.ComplimentaryUntil is null
        or now <= profile.ComplimentaryUntil

if profile.Status in [Active, Trialing]:
    return profile.CurrentPeriodEnd is null
        or now <= profile.CurrentPeriodEnd + GracePeriodDays

if profile.Status == PastDue:
    return profile.CurrentPeriodEnd is not null
        and now <= profile.CurrentPeriodEnd + GracePeriodDays

if profile.Status == Canceled:
    return profile.CurrentPeriodEnd is not null
        and now <= profile.CurrentPeriodEnd + GracePeriodDays

return false
```

### 5.2 `IsArticleFullyVisible(article, user, now)`

```
if not MembershipOptions.Enabled:
    return true

if article.PublishedAt is null:
    return false

if article.AccessLevel == Public:
    return true

if article.AccessLevel == Registered:
    return user is not null

if article.AccessLevel == Premium:
    if HasFullAccess(user, now):
        return true
    return now >= article.PublishedAt + TeaserDelayDays

return false
```

**Begründung:** `Public` und `Registered` behalten ihre bisherige Semantik.
Die neue zeitbasierte Paywall gilt für `Premium`-Artikel. Nach
`TeaserDelayDays` werden Premium-Artikel für alle frei. Die Kulanzzeit
`GracePeriodDays` gilt für aktive, gekündigte und zahlungsverzögerte Abos.

## 6. Stripe-Integration

### 6.1 Checkout

- Endpunkt: `POST /membership/checkout`
- Nur für angemeldete Benutzer.
- Bei `Membership:Enabled = false`: HTTP 404.
- Modus: `subscription`.
- `ClientReferenceId` und `Metadata["userId"]` = interne Benutzer-ID.
- Preis-ID aus `PriceIdMonthly` bzw. `PriceIdYearly`.
- Weiterleitung zur von Stripe gelieferten Checkout-URL.
- Die Erfolgsseite ändert **keinen** Status.

### 6.2 Webhook

- Endpunkt: `POST /webhooks/stripe`
- Rohtext lesen, Signatur mit `Stripe.EventUtility.ConstructEvent` prüfen.
- Bei ungültiger Signatur: HTTP 400, keine Verarbeitung.
- Idempotenz über `StripeEvent.StripeEventId`; bereits verarbeitete Events
  antworten mit HTTP 200 und werden nicht erneut angewendet.
- Zuordnung zum Benutzer ausschließlich über `ClientReferenceId` oder
  `Metadata["userId"]`. Ohne Zuordnung wird kein Status gesetzt.
- Bei `Membership:Enabled = false`: HTTP 404.
- Verarbeitete Events:
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `invoice.paid`
  - `invoice.payment_failed`
- Gespeicherte Felder: `StripeCustomerId`, `StripeSubscriptionId`, `Status`,
  `CurrentPeriodEnd`, `CancelAtPeriodEnd`, `LastSyncedAt`.

### 6.3 Kundenportal

- Endpunkt: `POST /membership/portal`
- Nur für angemeldete Benutzer.
- Nur wenn `StripeCustomerId` vorhanden ist.
- Erstellt eine Stripe-Billing-Portal-Session und leitet weiter.
- Bei `Membership:Enabled = false`: HTTP 404.

### 6.4 Login-Sync

- Beim Login wird geprüft, ob `LastSyncedAt` älter als
  `SyncOnLoginIfOlderThanHours` ist.
- Wenn ja: Abo aktuell von Stripe laden und `MembershipProfile` aktualisieren.
- Schlägt der Abruf fehl: letzten bekannten Status verwenden, Fehler loggen,
  nicht an den Benutzer weiterreichen.

### 6.5 Hintergrundabgleich

- `MembershipSyncService : BackgroundService`.
- Intervall: `SyncIntervalHours`.
- Läuft nicht, wenn `Membership:Enabled = false`.
- Lädt Profile mit `StripeSubscriptionId` in Batches (max. 50 pro Durchlauf).
- Aktualisiert Status, `CurrentPeriodEnd`, `CancelAtPeriodEnd`, `LastSyncedAt`.
- Fehler pro Batch loggen, restliche Batches weiterverarbeiten.

## 7. Admin-Freigaben

- Seite `/Admin/Memberships` mit Liste, Suche und Filter:
  - Alle
  - Mitglieder (aktives Abo)
  - Freigeschaltet (Complimentary)
  - Basis (kein Zugang)
- Aktion „Freischalten“ mit optionalem Ablaufdatum und Notiz.
- Aktion „Freigabe entziehen“.
- Jede Änderung schreibt `MembershipAuditLog`.
- Manueller Sync-Button pro Benutzer.
- Alle Endpunkte serverseitig mit `RequireAdmin` geschützt.
- Bei `Membership:Enabled = false`: Menüpunkt ausgeblendet, Seiten liefern 404.

## 8. Testfälle

### 8.1 Zugriff und Teaser

| ID | Szenario | Erwartung |
|---|---|---|
| T01 | Funktion aus, anonymer Besucher, Premium-Artikel | Volltext sichtbar, Verhalten unverändert |
| T02 | Funktion an, anonymer Besucher, Premium-Artikel innerhalb `TeaserDelayDays` | Nur Teaser, kein Volltext im HTML |
| T03 | Funktion an, anonymer Besucher, Premium-Artikel nach `TeaserDelayDays` | Volltext sichtbar |
| T04 | Funktion an, angemeldeter Nicht-Mitglied, Premium-Artikel innerhalb `TeaserDelayDays` | Nur Teaser |
| T15 | Funktion an, aktives Mitglied, Premium-Artikel | Volltext sichtbar |

### 8.2 Admin-Freigaben

| ID | Szenario | Erwartung |
|---|---|---|
| T05 | Admin-Freigabe mit Ablaufdatum | Zugang bis Ablauf, danach kein Zugang |
| T06 | Admin entzieht Freigabe | Kein Zugang, Audit-Log-Eintrag vorhanden |

### 8.3 Stripe

| ID | Szenario | Erwartung |
|---|---|---|
| T07 | Checkout-Session | Korrekte `ClientReferenceId`, `Metadata["userId"]`, Preis-ID |
| T08 | Webhook mit ungültiger Signatur | HTTP 400, keine Statusänderung |
| T09 | `checkout.session.completed` | Profil aktiv, Stripe-IDs gespeichert |

### 8.4 Portal, Login-Sync und Hintergrundjob

| ID | Szenario | Erwartung |
|---|---|---|
| T10 | Portal-Session mit `StripeCustomerId` | Weiterleitung zur Portal-URL |
| T11 | Login-Sync bei veraltetem `LastSyncedAt` | Profil wird aktualisiert |
| T12 | Login-Sync schlägt fehl | Letzter bekannter Status bleibt erhalten |
| T13 | Hintergrundabgleich | Aktive Abos werden aktualisiert |
| T14 | Hintergrundabgleich bei deaktivierter Funktion | Job führt keine Stripe-Aufrufe aus |

### 8.5 Zusätzliche Unit-Tests

- Admin hat immer Zugang.
- Freigabe ohne Ablaufdatum ist unbegrenzt.
- Aktives Abo innerhalb der Kulanzzeit hat Zugang.
- Aktives Abo nach der Kulanzzeit hat keinen Zugang.
- Gekündigtes Abo hat bis Periodenende + Kulanz Zugang.
- `PastDue` hat bis Periodenende + Kulanz Zugang.
- Premium-Artikel vor und nach `TeaserDelayDays`.
- `Public`- und `Registered`-Artikel behalten ihre Semantik.

## 9. Phasen

1. **Phase 1 — Vorbereitung ohne Stripe:** Options, Konfiguration, Entitäten,
   Migration, `IMembershipService`, Unit-Tests. Funktion bleibt aus.
2. **Phase 2 — Teaser und Wartezeit:** Artikel-Lesebereich, Teaser-UI,
   serverseitige Auslassung des Volltexts, Caching-Prüfung, Tests T01–T04/T15.
3. **Phase 3 — Admin-Freigaben:** Admin-Seite, Freigabe/Entzug, Audit-Log,
   manueller Sync-Platzhalter, Tests T05/T06.
4. **Phase 4 — Checkout und Webhooks:** Stripe.net, Checkout-Endpunkt,
   Webhook-Endpunkt, Idempotenz, Tests T07–T09.
5. **Phase 5 — Portal, Login-Sync, Tagesabgleich:** Portal-Endpunkt,
   Login-Sync, `MembershipSyncService`, Kontobereich, Tests T10–T14.
6. **Phase 6 — Härtung und Abschluss:** Autorisierung, Geheimnisprüfung,
   vollständiger Testlauf, Betriebsdokumentation, Abnahmeliste.

## 10. Nicht-funktionale Anforderungen

- **NF-05 Caching:** Artikelseiten dürfen nicht access-unabhängig gecacht
  werden. Falls Output-Caching ergänzt wird, muss der Cache-Schlüssel die
  Zugriffsstufe enthalten oder für Artikelseiten deaktiviert sein.
- **Sicherheit:** Keine Stripe-Geheimnisse in Repository, Logs oder
  Fehlermeldungen. Webhook-Signatur ist Pflicht.
- **Performance:** Hintergrundabgleich in Batches, keine unbegrenzten
  Stripe-Aufrufe pro Request.
- **Beobachtbarkeit:** Fehler beim Login-Sync und im Hintergrundjob werden
  geloggt, ohne Geheimnisse preiszugeben.
- **Rechtliches:** AGB, Widerruf und Datenschutz werden nicht erfunden.
  Platzhalter und offene Punkte bleiben bestehen.

## 11. Abweichungen und bewusste Entscheidungen

1. **Konsolidierung:** `MembershipProfile` ist die einzige Abo-Quelle. Die alte
   `Subscription`-Tabelle und der alte Abo-Flow wurden entfernt.
2. **Feld-Mapping:** `Summary` und `MainImageUrl` werden auf die vorhandenen
   Felder `Excerpt` und `TitleImageUrl` abgebildet. Keine Duplikate.
3. **Stripe.net:** Wird für die neue Funktion eingeführt. Die bestehende
   Raw-REST-Integration bleibt für den alten Flow bestehen.
4. **Feature-Flag:** `Membership:Enabled` ist unabhängig von
   `Features:MonetizationEnabled`. Bei `false` bleibt das alte Verhalten aktiv.

## 12. Offene Punkte

- Rechtliche Texte (AGB, Widerruf, Datenschutz) sind Platzhalter.
- Steuerliche Behandlung von Mitgliedschaften ist zu klären.
- Preisgestaltung und Stripe-Produkte werden im Stripe-Dashboard angelegt.
