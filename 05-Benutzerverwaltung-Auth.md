# 05 — Benutzerverwaltung & Auth

Referenzierte Entitäten aus `DataSchema.md`: `User`.

## Funktionale Anforderungen

### Registrierung & Login
- Basis: ASP.NET Core Identity.
- Registrierung per E-Mail/Passwort, E-Mail-Bestätigung verpflichtend (`EmailConfirmed`) vor voller Nutzung (Kommentieren, Abo).
- Login klassisch, optional erweiterbar um externe Provider (Google/GitHub) — v1: nicht verpflichtend, aber Identity-Struktur lässt das offen.

### Rollen
| Rolle | Rechte |
|---|---|
| `Reader` | Lesen, kommentieren (nach E-Mail-Bestätigung) |
| `Premium` | wie `Reader`, zusätzlich Zugriff auf `IsPremium`-Artikel (gesteuert über aktive `Subscription`, nicht direkt über die Rolle — siehe `04-Monetarisierung.md`) |
| `Moderator` | wie `Reader`, zusätzlich Zugriff auf Kommentar-Moderationsansicht (`Report`-Verwaltung, Kommentar-Status ändern) |
| `Admin` | vollständiger Zugriff, inkl. Artikel-Verwaltung, Nutzerverwaltung |

> Hinweis: Premium-Zugriff wird technisch **nicht** über `User.Role = Premium` als alleinige Prüfung realisiert, sondern über `User.SubscriptionStatus` bzw. eine aktive `Subscription`. Die Rolle `Premium` kann optional zusätzlich gesetzt werden, falls rollenbasierte UI-Steuerung (z. B. `[Authorize(Roles = ...)]`) gewünscht ist — Autorisierungs-Policy sollte primär auf dem Abo-Status basieren, um Verzögerungen zwischen Zahlungsstatus und Rollenzuweisung zu vermeiden.

### Konto-Löschung
- Nutzer kann Account-Löschung beantragen → Soft-Delete (`DeletedAt`), personenbezogene Felder (E-Mail, DisplayName) werden anonymisiert, nutzergenerierte Inhalte (Artikel, Kommentare) bleiben strukturell erhalten, aber Autor wird als "Gelöschter Nutzer" angezeigt.

## Akzeptanzkriterien

- [ ] Nicht bestätigte E-Mail-Adressen können nicht kommentieren.
- [ ] Rollenprüfung für Moderationsansicht ist serverseitig erzwungen, nicht nur UI-seitig ausgeblendet.
- [ ] Paywall-Zugriffsprüfung nutzt den aktuellen Abo-Status, nicht ausschließlich die Rolle.
- [ ] Account-Löschung anonymisiert personenbezogene Daten, ohne Kommentar-/Artikel-Historie strukturell zu zerstören.
