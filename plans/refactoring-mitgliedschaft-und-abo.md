# Refactoring: Alten Abo-Workflow entfernen, Membership konsolidieren, Admin-Abo-Übersicht

## 1. Ausgangslage

Es existieren zwei parallele Abo-Modelle:

1. **Alter Workflow**
   - `Subscription`, `SubscriptionStatus`, `UserSubscriptionStatus`
   - `User.SubscriptionStatus`, `User.Subscriptions`
   - `SubscriptionService`/`ISubscriptionService`
   - `IStripeService`/`StripeService`/`FakeStripeService`
   - `PaymentOptions`, `StripeEventParser`, `StripeSignatureVerifier`
   - `StripeWebhookController` unter `/api/stripe/webhook`
   - Razor Pages `Membership/Checkout` und `Membership/CheckoutComplete`
   - `PremiumAuthorizationHandler` und `Policies.PremiumAccess`
   - `Stripe`-Sektion in `appsettings.json`

2. **Neuer Membership-Workflow**
   - `MembershipProfile`, `StripeEvent`, `MembershipAuditLog`
   - `MembershipService`, `StripeMembershipService`, `MembershipWebhookHandler`
   - `MembershipSyncService`, `MembershipSyncBackgroundService`
   - Endpunkte `/membership/checkout`, `/membership/portal`, `/webhooks/stripe`
   - Admin-Seite `/Admin/Memberships`

Da es nie Abonnenten im alten Workflow gab, wird dieser vollständig entfernt.
Der neue Membership-Workflow wird die einzige Abo-Quelle.

## 2. Zielarchitektur

- `MembershipProfile` ist die einzige persistente Abo-Tabelle.
- `MembershipProfile.PlanId` speichert die Stripe-Preis-ID für die Admin-Übersicht.
- `Donation` bleibt erhalten; die Aufzeichnung wandert in einen eigenen
  `DonationService`.
- `Membership:Enabled` bleibt der Hauptschalter.
- Bei `Membership:Enabled = false` gilt:
  - `Public`-Artikel sind frei.
  - `Registered`-Artikel benötigen eine Anmeldung.
  - `Premium`-Artikel sind nur für Admins sichtbar.
  - Das entspricht dem praktischen Altverhalten, da es keine Abonnenten gab.
- Die Artikelseite nutzt immer `IMembershipService.IsArticleFullyVisibleAsync`;
  die alte `ArticleAccessLevel`-Verzweigung und `Policies.PremiumAccess` entfallen.
- Der Admin-Bereich `/Admin/Memberships` wird zur Abo-Übersicht ausgebaut:
  - Zusammenfassung: Gesamt, Aktiv, Zahlung offen, Gekündigt, Freigeschaltet, Basis.
  - Tabelle mit Plan, Laufzeit, Kündigung zum Periodenende, letztem Sync.
  - Filter: Alle, Aktiv, Zahlung offen, Gekündigt, Freigeschaltet, Basis.
  - Manueller Sync.

## 3. Phase A — Alten Workflow entfernen

### Löschen

- `src/BlogCms.Domain/Entities/Subscription.cs`
- `src/BlogCms.Domain/Enums/SubscriptionStatus.cs`
- `src/BlogCms.Domain/Enums/UserSubscriptionStatus.cs`
- `src/BlogCms.Infrastructure/Payments/SubscriptionService.cs`
- `src/BlogCms.Infrastructure/Payments/IStripeService.cs`
- `src/BlogCms.Infrastructure/Payments/StripeService.cs`
- `src/BlogCms.Infrastructure/Payments/FakeStripeService.cs`
- `src/BlogCms.Infrastructure/Payments/PaymentOptions.cs`
- `src/BlogCms.Infrastructure/Payments/StripeEventParser.cs`
- `src/BlogCms.Infrastructure/Data/Configurations/SubscriptionConfiguration.cs`
- `src/BlogCms.Web/Controllers/StripeWebhookController.cs`
- `src/BlogCms.Web/Pages/Membership/Checkout.cshtml`
- `src/BlogCms.Web/Pages/Membership/Checkout.cshtml.cs`
- `src/BlogCms.Web/Authorization/PremiumAuthorizationHandler.cs`
- `src/BlogCms.Web/Authorization/PremiumRequirement.cs` (falls vorhanden)
- `tests/BlogCms.Tests/PaymentTests.cs` (Subscription-Tests; Donation-Test wird verschoben)

### Anpassen

- `User`: `SubscriptionStatus` und `Subscriptions` entfernen.
- `AppDbContext`: `DbSet<Subscription>` entfernen.
- `Program.cs`: alte Payment-DI, `PaymentOptions`, `IStripeService`,
  `ISubscriptionService`, `Policies.PremiumAccess` und
  `PremiumAuthorizationHandler` entfernen.
- `appsettings.json`: `Stripe`-Sektion entfernen.
- `Account/Manage/Index`: alten „Abo-Status“ entfernen; neue Membership-Anzeige
  bleibt.
- `Admin/Users/Index`: alte „Abo-Status“-Spalte entfernen.
- `Membership/CheckoutComplete`: als reine Erfolgsseite ohne
  `ISubscriptionService` umbauen; kein Statuswechsel.
- `Membership/Index` und `Donate/Index`: Redirects auf `/Unterstuetzer`
  beibehalten (alte Links).
- `PremiumAuthorizationHandler`-Registrierung und `Policies.PremiumAccess`
  entfernen.

### Donation erhalten

- Neuer `IDonationService`/`DonationService` in
  `src/BlogCms.Infrastructure/Payments/DonationService.cs`.
- Methode `RecordDonationAsync` aus `SubscriptionService` übernehmen.
- DI-Registrierung in `Program.cs`.
- Test `RecordDonation_AllowsAnonymousDonations` nach
  `tests/BlogCms.Tests/DonationServiceTests.cs` verschieben.

## 4. Phase B — Membership als einzige Abo-Quelle

### Datenmodell

- `MembershipProfile.PlanId` (string?) ergänzen.
- `MembershipStripeSubscription` um `PlanId` erweitern.
- `MembershipStripeSnapshot` um `PlanId` erweitern.
- `MembershipStripeEvent` um `PlanId` erweitern.
- `MembershipStripeEventParser`: `planId` aus `metadata.planId` oder
  `items.data[0].price.id` lesen.
- `StripeMembershipService.GetSubscriptionAsync`: Preis-ID aus
  `subscription.Items.Data[0].Price.Id` übernehmen.
- `MembershipWebhookHandler`: `PlanId` an den Snapshot übergeben.
- `MembershipSyncService`: `PlanId` an den Snapshot übergeben.
- `MembershipService.ApplyStripeSnapshotAsync`: `PlanId` speichern.
- `MembershipService.GetForAdminAsync`: `PlanId` über `MembershipAdminRow`
  verfügbar machen.

### Migration

Neue Migration `RemoveLegacySubscriptionAndAddMembershipPlan`:

1. `MembershipProfiles.PlanId` als `character varying(200)` nullable hinzufügen.
2. `Subscriptions`-Tabelle droppen.
3. `AspNetUsers.SubscriptionStatus` droppen.
4. `Down`: `SubscriptionStatus` und `Subscriptions` ohne Daten wieder anlegen.

### Zugriffslogik

- `MembershipService.HasFullAccessAsync`:
  - `Enabled = false` → nur `UserRole.Admin` hat Zugriff.
  - `Enabled = true` → bisherige Logik.
- `MembershipService.IsArticleFullyVisibleAsync`:
  - `Enabled = false`:
    - `Public` → true
    - `Registered` → `user is not null`
    - `Premium` → `user?.Role == Admin`
  - `Enabled = true` → bisherige Logik.
- `Articles/Details.cshtml.cs`:
  - `IAuthorizationService` und `Policies.PremiumAccess` entfernen.
  - Immer `_membership.IsArticleFullyVisibleAsync` verwenden.
  - `RequiresLogin`/`RequiresMembership`/`TeaserAvailableAt` wie bisher setzen.
- `PremiumAuthorizationHandler` und `Policies.PremiumAccess` entfernen.

## 5. Phase C — Admin-Abo-Übersicht

### `MembershipFilter`

Erweitern um:

- `Active`
- `PastDue`
- `Canceled`

Bestehende Werte:

- `All`
- `Complimentary`
- `Basic`

`Member` kann entfallen oder als Alias für `Active` dienen. Die UI verwendet
klare Labels.

### `MembershipAdminRow`

Erweitern um:

- `PlanId`
- `LastSyncedAt`
- `CancelAtPeriodEnd`

### Admin-Seite

- Zusammenfassungskarten:
  - Gesamt
  - Aktiv
  - Zahlung offen
  - Gekündigt
  - Freigeschaltet
  - Basis
- Tabelle:
  - Benutzer
  - E-Mail
  - Rolle
  - Status
  - Plan (Preis-ID oder „–“)
  - Laufzeit bis
  - Kündigung zum Periodenende (Ja/Nein)
  - Freigabe bis
  - Letzter Sync
  - Aktionen (Freischalten/Entziehen/Sync)
- Filter-Dropdown mit den neuen Filtern.
- Suchfeld bleibt.
- Seite heißt „Mitgliedschaften & Abos“.

## 6. Phase D — Tests

### Entfernen/Ersetzen

- `PaymentTests.cs`:
  - Subscription-Tests entfernen.
  - Donation-Test nach `DonationServiceTests.cs` verschieben.
  - `FakeStripeServiceTests` entfernen.

### Anpassen

- `MembershipAccessTests`:
  - `HasFullAccess_FeatureDisabled_ReturnsTrue` ersetzen durch
    `HasFullAccess_FeatureDisabled_OnlyAdmin`.
  - `IsArticleFullyVisible_FeatureDisabled_ReturnsTrue` ersetzen durch
    `IsArticleFullyVisible_FeatureDisabled_PublicRegisteredPremium`.
- `MembershipStripeTests`:
  - `PlanId` in Snapshot/Event ergänzen.
- `MembershipSyncTests`:
  - `PlanId` in `MembershipStripeSubscription` ergänzen.
- Neue Tests:
  - `PlanId` wird aus dem Stripe-Snapshot gespeichert.
  - Admin-Filter `Active`, `PastDue`, `Canceled`.
  - Admin-Zusammenfassung zählt korrekt.
  - DonationService zeichnet anonyme Spenden auf.

## 7. Phase E — Dokumentation aktualisieren

### Zu aktualisieren

- `README.md`: alten Abo-Flow entfernen; Membership als einzige Abo-Funktion.
- `DataSchema.md`:
  - `Subscription` und `User.SubscriptionStatus` entfernen.
  - `MembershipProfile` inkl. `PlanId`, `StripeEvent`,
    `MembershipAuditLog` vollständig dokumentieren.
  - `Donation` bleibt.
- `04-Monetarisierung.md`: neues Membership-Modell, keine alte
  `Subscription`-Tabelle.
- `docs/03-blog-nutzen.md`: neue URLs (`/Unterstuetzer`, `/Account/Manage`),
  keine `/Membership/Checkout`-Verweise.
- `docs/09-mitgliedschaft-stripe.md`: Koexistenz-Abschnitt entfernen; neue
  Admin-Abo-Übersicht und `PlanId` dokumentieren.
- `docs/10-abnahme-mitgliedschaft.md`: Testanzahl und neue Admin-Tests
  aktualisieren.
- `docs/11-bezahlfunktionen-erklaert.md`: alten Abo-Flow aus der
  Funktionsliste entfernen; Admin-Abo-Übersicht ergänzen.
- `docs/01-rollen-und-admin.md`: Premium-Rolle und `/Unterstuetzer`.
- `docs/06-lizenzen-und-pakete.md`: `Stripe.net` 53.1.0 (Apache-2.0)
  aufnehmen.
- `RUNNING.md`: alte `Stripe`-Konfiguration und `FakeStripeService`-Hinweise
  entfernen; `Membership`-Konfiguration dokumentieren.
- `appsettings.json`: `Stripe`-Sektion entfernen.
- `01-Content-Verwaltung.md`: Premium-CTA auf `/Unterstuetzer`.
- `05-Benutzerverwaltung-Auth.md`: Premium-Rolle ohne alten Abo-Status.
- `00-Uebersicht.md`: Membership/Spenden-Beschreibung aktualisieren.
- `docs/README.md`: Beschreibungen der Membership-Dokumente aktualisieren.

### Zu löschen/ersetzen

- Es gibt keine separate alte Workflow-Datei. Die alten Abschnitte in
  `04-Monetarisierung.md`, `docs/03-blog-nutzen.md`, `docs/09`,
  `docs/11`, `RUNNING.md` und `README.md` werden entfernt bzw. ersetzt.
- `Membership/Checkout`-Seite wird gelöscht.
- `Membership/CheckoutComplete` bleibt als reine Erfolgsseite.

## 8. Phase F — Abschluss

- Build und vollständige Testsuite.
- Secret-Scan.
- Prüfen, dass keine Referenzen auf `Subscription`, `UserSubscriptionStatus`,
  `IStripeService`, `PaymentOptions`, `StripeWebhookController` oder
  `Membership/Checkout` übrig sind.
- Abschlussbericht mit geänderten Dateien und Testergebnis.

## 9. Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Datenverlust durch Drop der `Subscriptions`-Tabelle | Ausdrücklich gewünscht; es gab keine Abonnenten. `Down`-Migration legt die Struktur wieder an. |
| `Premium`-Artikel bei deaktivierter Funktion | Admin-only, dokumentiert; entspricht dem praktischen Altverhalten. |
| Alte Links auf `/Membership/Checkout` | Redirect auf `/Unterstuetzer` beibehalten oder 404; Entscheidung: Redirect. |
| Donation-Funktion bricht weg | `DonationService` aus `SubscriptionService` herauslösen. |
| Stripe.net-Preis-ID nicht verfügbar | `PlanId` bleibt nullable; Admin zeigt „–“. |
| Doku-Widersprüche | `DataSchema.md` zuerst aktualisieren, dann Feature-Dokus. |
