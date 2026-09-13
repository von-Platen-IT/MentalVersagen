# 05 — Benutzerverwaltung & Auth

Referenzierte Entitäten aus `DataSchema.md`: `User`.

## Funktionale Anforderungen

### Registrierung & Login
- Basis: ASP.NET Core Identity.
- Registrierung per E-Mail/Passwort, E-Mail-Bestätigung verpflichtend (`EmailConfirmed`) vor voller Nutzung (Kommentieren, Abo).
- Login klassisch, optional erweiterbar um externe Provider (Google/GitHub) — v1: nicht verpflichtend, aber Identity-Struktur lässt das offen.

### Rollen

Mapping der FeatureFix1-Rollen (BR-010) auf die Implementierung: Viewer ≙ `Reader`,
Autor ≙ `Author`, Administrator ≙ `Admin`. `Moderator` und `Premium` bleiben als
zusätzliche, projekt-spezifische Rollen erhalten.

| Rolle | FeatureFix1 | Rechte |
|---|---|---|
| `Reader` | Viewer | Lesen, kommentieren, bewerten (nach E-Mail-Bestätigung) |
| `Author` | Autor | wie `Reader`, zusätzlich **eigene** Beiträge erstellen, bearbeiten, als Entwurf speichern, veröffentlichen und planen; eigene Beiträge verwalten; Kommentare der eigenen Beiträge moderieren. Keine fremden Beiträge. |
| `Premium` | — | wie `Reader`; Zugriff auf Premium-Artikel (gesteuert über aktive `Subscription`, nicht allein über die Rolle — siehe `04-Monetarisierung.md`) |
| `Moderator` | — | wie `Reader`, zusätzlich Zugriff auf Kommentar-Moderationsansicht (`Report`-Verwaltung, Kommentar-Status ändern) |
| `Admin` | Administrator | vollständiger Zugriff, inkl. aller Beiträge, Kommentar-Moderation/-Auszeichnung, Linklisten, Nutzer-/Rollenverwaltung |

> Hinweis: Premium-Zugriff wird technisch **nicht** über `User.Role = Premium` als alleinige Prüfung realisiert, sondern über `User.SubscriptionStatus` bzw. eine aktive `Subscription`. Die Rolle `Premium` kann optional zusätzlich gesetzt werden, falls rollenbasierte UI-Steuerung gewünscht ist — die Autorisierungs-Policy basiert primär auf dem Abo-Status.

### Autorisierung
- Serverseitige Policies: `PremiumAccess` (Abo-getrieben), `RequireModerator`, `RequireAdmin`
  und **`RequireAuthor`** (`Author` oder `Admin`).
- **Besitzregel:** `Author` darf ausschließlich eigene Beiträge bearbeiten/verwalten;
  `Admin` unterliegt keiner Besitzbeschränkung.
- Zugriffsstufen von Beiträgen (`Public`/`Registered`/`Premium`) werden zusätzlich zur
  Rolle geprüft (siehe `01-Content-Verwaltung.md`, `04-Monetarisierung.md`).

### Konto-Löschung
- Nutzer kann Account-Löschung beantragen → Soft-Delete (`DeletedAt`), personenbezogene Felder (E-Mail, DisplayName) werden anonymisiert, nutzergenerierte Inhalte (Artikel, Kommentare) bleiben strukturell erhalten, aber Autor wird als "Gelöschter Nutzer" angezeigt.

## Akzeptanzkriterien

- [ ] Nicht bestätigte E-Mail-Adressen können nicht kommentieren.
- [ ] Rollenprüfung für Moderationsansicht ist serverseitig erzwungen, nicht nur UI-seitig ausgeblendet.
- [ ] Paywall-Zugriffsprüfung nutzt den aktuellen Abo-Status, nicht ausschließlich die Rolle.
- [ ] Ein `Author` kann nur eigene Beiträge bearbeiten; fremde Beiträge werden serverseitig verweigert.
- [ ] Zugriffsstufe `Registered` erfordert eine Anmeldung, `Premium` eine aktive Berechtigung.
- [ ] Account-Löschung anonymisiert personenbezogene Daten, ohne Kommentar-/Artikel-Historie strukturell zu zerstören.
