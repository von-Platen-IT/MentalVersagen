# 12 — Administration von Mentalversagen

Diese Anleitung richtet sich an **Administratorinnen und Administratoren** des
Blogs. Sie beschreibt den Admin-Zugang, alle Verwaltungsseiten, die wichtigsten
Betriebseinstellungen und die häufigsten Fehlerbilder.

Technische Details stehen in [`09-mitgliedschaft-stripe.md`](09-mitgliedschaft-stripe.md),
[`05-management-api.md`](05-management-api.md) und [`04-mailversand.md`](04-mailversand.md).
Das Autoren-Handbuch liegt in [`13-autoren-handbuch.md`](13-autoren-handbuch.md).

---

## 1. Admin-Zugang

### 1.1 Konto und E-Mail-Bestätigung

1. Auf `/Account/Register` registrieren.
2. Die Bestätigungsmail öffnen. Im Dev-Modus liegt sie als HTML-Datei unter
   `src/BlogCms.Web/bin/Debug/net10.0/App_Data/emails/`.
3. Den Link zur E-Mail-Bestätigung aufrufen.

### 1.2 Erste Admin-Rolle (Bootstrap)

Es gibt **keinen vorkonfigurierten Admin**. Die erste Admin-Rolle wird einmalig
per SQL vergeben:

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'deine@adresse.example' AND r."Name" = 'Admin'
ON CONFLICT DO NOTHING;
```

Danach ab- und wieder anmelden, damit die Rolle im Auth-Cookie aktiv wird.

### 1.3 Weitere Rollen

Sobald ein Admin existiert, werden alle Rollen komfortabel unter
**`/Admin/Users`** vergeben. Der Wechsel synchronisiert die Identity-Rolle mit
dem Domänenfeld `User.Role`.

| Rolle | Typische Aufgabe |
|---|---|
| `Reader` | Lesen, kommentieren, bewerten |
| `Author` | Eigene Beiträge erstellen und verwalten |
| `Premium` | Optional; Premium-Zugriff läuft über `MembershipProfile` |
| `Moderator` | Kommentar-Moderation unter `/Moderation` |
| `Admin` | Vollzugriff auf alle Verwaltungsseiten |

---

## 2. Verwaltungsübersicht

Die Navigation **„Verwaltung“** erscheint für `Admin` (und teilweise für
`Author`). Erreichbare Seiten:

| Seite | Zweck | Rolle |
|---|---|---|
| `/Admin/Articles` | Artikelübersicht, Statuswechsel, Kategorie ändern, Löschen | Author/Admin |
| `/Admin/Articles/Create` | Neuen Artikel anlegen | Author/Admin |
| `/Admin/Articles/Edit/{id}` | Artikel bearbeiten | Author/Admin |
| `/Admin/Categories` | Kategorien anlegen, bearbeiten, löschen, Farbe | Admin |
| `/Admin/LinkLists` | Redaktionelle Linklisten verwalten | Admin |
| `/Admin/Users` | Benutzer und Rollen verwalten | Admin |
| `/Admin/Memberships` | Abo-Übersicht, Freigaben, Sync | Admin |
| `/Admin/Settings` | Beiträge pro Liste | Admin |
| `/Admin/Activity` | Aktivitätsauswertung | Admin |
| `/Moderation` | Kommentar-Moderation | Moderator/Admin |

---

## 3. Artikelverwaltung (`/Admin/Articles`)

- **Übersicht:** zeigt alle Beiträge, die der Benutzer verwalten darf.
  `Author` sieht nur eigene Beiträge, `Admin` alle.
- **Filter:** Status (`Draft`, `Scheduled`, `Published`, `Archived`) und
  Freitextsuche in Titel, Slug und Kurzbeschreibung.
- **Statuswechsel:** direkt in der Tabelle. Beim Veröffentlichen prüft das
  System Titelbild, Kurzbeschreibung und Inhalt.
- **Kategorie ändern:** nur `Admin`; die Auswahl wird sofort gespeichert.
- **Löschen:** Soft-Delete. Der Beitrag bleibt in der Datenbank, verschwindet
  aber aus allen Listen.
- **Bearbeiten:** öffnet das vollständige Formular (siehe Autoren-Handbuch).

---

## 4. Kategorien (`/Admin/Categories`)

- **Anlegen:** Name ist Pflicht; Slug wird bei Bedarf automatisch erzeugt.
- **Bearbeiten:** Name, Slug, Beschreibung, Disclaimer, **Farbe** und
  Reihenfolge.
- **Farbe:** Hex-Farbe (`#RRGGBB`) für den 3-px-Rahmen um Beitragseinträge in
  Listen. Der Farbwähler zeigt die Farbe direkt an.
- **Reihenfolge:** kleinere Werte erscheinen zuerst.
- **Löschen:** Zugehörige Artikel werden automatisch der Systemkategorie
  `undefined` zugeordnet.
- **Systemkategorie `undefined`:** kann weder gelöscht noch umbenannt werden.
  Beschreibung, Disclaimer, Farbe und Reihenfolge sind editierbar.

---

## 5. Linklisten (`/Admin/LinkLists`)

- Linklisten sind redaktionell geordnete Sammlungen von Beiträgen.
- **Anlegen:** Titel, optionaler Slug und Beschreibung.
- **Bearbeiten:** Beiträge hinzufügen, entfernen und in der Reihenfolge
  verschieben.
- **Öffentliche Darstellung:** `/LinkLists/{slug}`.
- **Einbetten:** `/LinkLists/Embed/{slug}` liefert eine einbettbare Ansicht.

---

## 6. Benutzer und Rollen (`/Admin/Users`)

- Zeigt alle nicht gelöschten Benutzer mit E-Mail, Anzeigename und Rolle.
- **Rolle ändern:** Auswahl im Dropdown; die Änderung wird sofort gespeichert.
- Der Wechsel entfernt bestehende Identity-Rollen und setzt die neue Rolle.
- Nach einer Rollenänderung muss sich der betroffene Benutzer neu anmelden.

---

## 7. Mitgliedschaften (`/Admin/Memberships`)

Voraussetzung: `Membership:Enabled = true`. Bei deaktivierter Funktion liefert
die Seite `404`.

- **Zusammenfassung:** Gesamt, Aktiv, Zahlung offen, Gekündigt,
  Freigeschaltet, Basis.
- **Tabelle:** Benutzer, Status, Tarif (`PlanId`), Laufzeit, Kündigung zum
  Periodenende, letzter Stripe-Sync.
- **Suche:** nach Name oder E-Mail.
- **Filter:** Alle, Aktiv, Zahlung offen, Gekündigt, Freigeschaltet, Basis.
- **Freigabe setzen:** kostenlose Freischaltung mit optionalem Ablaufdatum und
  Notiz.
- **Freigabe entziehen:** entfernt die kostenlose Freischaltung.
- **Manueller Sync:** gleicht das Profil mit Stripe ab.
- Jede Änderung wird in `MembershipAuditLog` protokolliert.

Details zu Stripe, Webhook und Konfiguration:
[`09-mitgliedschaft-stripe.md`](09-mitgliedschaft-stripe.md).

---

## 8. Einstellungen (`/Admin/Settings`)

- **Beiträge pro Liste:** legt fest, wie viele Beiträge gleichzeitig auf der
  Startseite, in der Suche und im REST-Grid (`/api/articles`) angezeigt werden.
- Erlaubter Bereich: 1–50; Standard: 6.
- Der Wert wird in der Tabelle `SiteSettings` gespeichert und ist sofort aktiv.

---

## 9. Aktivitätsauswertung (`/Admin/Activity`)

- **Zeitraum:** standardmäßig die letzten 30 Tage; frei wählbar.
- **Überblick:** Artikelaufrufe, erstellte Beiträge, Kommentare, Bewertungen.
- **Top-Beiträge:** meistgesehene und bestbewertete Beiträge.
- **Tagesverlauf:** Aufrufe pro Tag.
- **Aufrufverlauf pro Artikel:** „wie oft und wann“.
- **Letzte Ereignisse:** chronologische Liste der Aktivitäten.

---

## 10. Moderation (`/Moderation`)

- Zeigt offene Meldungen und ausstehende/geflaggte Kommentare.
- **Freigeben:** Kommentar wird sichtbar.
- **Ablehnen:** Kommentar wird auf `Rejected` gesetzt (Soft-Delete).
- **Meldungen verwerfen:** markiert die Meldungen als erledigt.
- Moderatoren sehen diese Seite; Autoren können zusätzlich Kommentare ihrer
  eigenen Beiträge auf der Artikelseite moderieren.

---

## 11. Betrieb und Konfiguration

### 11.1 Feature-Flags

| Variable | Standard | Wirkung bei `false` |
|---|---|---|
| `Features__MonetizationEnabled` | `true` | Mitgliedschafts-/Spenden-/Premium-CTAs werden nicht gerendert |
| `Features__LoginEnabled` | `true` | Anmelden-/Registrieren-Links werden nicht gerendert |

Die Flags steuern nur die UI-Sichtbarkeit; Routen und Policies bleiben aktiv.

### 11.2 Mitgliedschaft

| Variable | Bedeutung |
|---|---|
| `Membership__Enabled` | Hauptschalter (Standard `false`) |
| `Membership__SecretKey` | Stripe Secret Key |
| `Membership__WebhookSecret` | Signing Secret des Webhooks |
| `Membership__PriceIdMonthly` / `__PriceIdYearly` | Stripe-Preis-IDs |
| `Membership__TeaserDelayDays` | Tage bis Premium-Volltext frei wird |
| `Membership__GracePeriodDays` | Kulanzzeit nach Periodenende |

### 11.3 Management-API

| Variable | Bedeutung |
|---|---|
| `Api__Key` | Statischer API-Key; ohne Key ist die API geschlossen (401) |
| `Api__AuthorId` | UUID des Autors für API-Anlage |
| `Api__SwaggerUiEnabled` | Interaktive Swagger-Oberfläche unter `/swagger` |
| `Api__RateLimitPerMinute` | Anfragen-Limit pro Minute und IP |

Details: [`05-management-api.md`](05-management-api.md).

### 11.4 E-Mail

| Variable | Bedeutung |
|---|---|
| `Email__Provider` | `Smtp` oder `Dev` |
| `Email__Host`, `__Port`, `__UseStartTls` | SMTP-Verbindung |
| `Email__UserName`, `__Password` | SMTP-Zugangsdaten |
| `Email__FromAddress`, `__FromDisplayName` | Absender |

Details: [`04-mailversand.md`](04-mailversand.md).

### 11.5 Medien

| Variable | Standard | Bedeutung |
|---|---|---|
| `Media__Provider` | `Local` | `Local` oder `S3` |
| `Media__ArticleMaxBytes` | 10 MB | Upload-Limit für Artikelbilder |
| `Media__CommentMaxBytes` | 4 MB | Upload-Limit für Kommentarbilder |
| `Media__MaxEdgePixels` | 1600 | Maximale Bildkante |
| `Media__ConvertToWebP` | `true` | Konvertierung nach WebP |

### 11.6 Datenbank und Migrationen

```bash
# Migrationen anwenden
dotnet ef database update \
  -p src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj \
  -s src/BlogCms.Web/BlogCms.Web.csproj
```

Die App wendet Migrationen **nicht** automatisch an. Vor einem Deployment
daher explizit ausführen. Backups der PostgreSQL-Datenbank regelmäßig erstellen.

---

## 12. Sicherheit

- Alle Admin-Seiten sind serverseitig über Policies geschützt
  (`RequireAdmin`, `RequireModerator`, `RequireAuthor`).
- Die Management-API ist **Fail-Closed**: ohne `Api__Key` antwortet sie mit
  `401`.
- Stripe-Webhooks werden über die Signatur geprüft.
- Secrets gehören in Umgebungsvariablen oder User Secrets, **nicht** in
  `appsettings.json` oder das Repository.
- Der Premium-Zugriff wird serverseitig über `MembershipProfile` entschieden,
  nicht über die Rolle allein.

---

## 13. Troubleshooting

| Symptom | Ursache / Lösung |
|---|---|
| `/Admin/...` liefert `403` | Rolle fehlt oder Cookie veraltet → neu anmelden, Rolle unter `/Admin/Users` prüfen |
| `/Admin/Memberships` liefert `404` | `Membership:Enabled` ist `false` |
| Registrierung bricht ab | SMTP nicht konfiguriert und Dev-Pfad nicht beschreibbar → `Email`-Sektion prüfen |
| Newsletter-Bestätigung kommt nicht an | `Email:Provider`/SMTP-Zugangsdaten prüfen; im Dev-Modus Dateien unter `App_Data/emails` |
| Stripe-Webhook `400` | `Membership:WebhookSecret` stimmt nicht |
| Status wird nicht aktualisiert | `ClientReferenceId`/`Metadata["userId"]` prüfen; manuellen Sync auslösen |
| API `401` | `Api__Key` fehlt oder ist falsch |
| API `503` beim Anlegen | `Api__AuthorId` fehlt |
| Bilder werden nicht angezeigt | `Media:Provider` und Storage-Pfad prüfen; bei `Local` Schreibrechte auf `wwwroot/uploads` |
| Kategorie-Farbe erscheint nicht | Farbe muss `#RRGGBB` sein; Browser-Cache leeren |
