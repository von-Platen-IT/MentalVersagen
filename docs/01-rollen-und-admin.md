# Rollen & Admin („Root")

Diese Seite erklärt das Rollenmodell, wie man **Admin („Root")** wird und wie man
den eigenen **Admin-Status in der Datenbank prüft**.

## Rollenmodell

Beim Start legt die Anwendung die Rollen idempotent an — es gibt jedoch **keinen
vorkonfigurierten Admin-Benutzer**. Neue Konten starten immer als `Reader`.

| Rolle | Rechte | Sichtbarer Bereich |
|---|---|---|
| `Reader` | Lesen, kommentieren (nach E-Mail-Bestätigung) | `/Articles`, `/Articles/{slug}` |
| `Premium` | wie `Reader`, zusätzlich Premium-Artikel (gesteuert über aktives Abo, nicht allein über die Rolle) | `/Membership` |
| `Moderator` | wie `Reader`, zusätzlich Kommentar-Moderation | `/Moderation` |
| `Admin` | vollständiger Zugriff, inkl. Artikelverwaltung | `/Admin/Articles` |

Die Rechte werden **serverseitig** über Autorisierungs-Policies erzwungen
(`RequireModerator`, `RequireAdmin`, `PremiumAccess`) — nicht nur über
ausgeblendete UI-Elemente.

## Admin („Root") werden

### 1. Konto registrieren und E-Mail bestätigen

Die Anmeldung erfordert eine bestätigte E-Mail-Adresse
(`SignIn.RequireConfirmedEmail`). Deshalb zuerst regulär registrieren:

1. `http://localhost:5080/Account/Register` aufrufen und das Formular ausfüllen
   (Anzeigename, E-Mail, Passwort mit mindestens 10 Zeichen).
2. Die erzeugte Bestätigungsmail wird im Dev-Modus **nicht versendet**, sondern
   als HTML-Datei gespeichert:

   ```
   src/BlogCms.Web/bin/Debug/net10.0/App_Data/emails/
   ```

3. Die neueste `.html`-Datei öffnen und den Link „E-Mail-Adresse bestätigen"
   (`/Account/ConfirmEmail?userId=…&code=…`) im Browser aufrufen.

### 2. Die Rolle `Admin` in der Datenbank zuweisen

Maßgeblich für die Autorisierung ist die Identity-Tabelle `AspNetUserRoles`.
Die Zuordnung erfolgt über SQL (es gibt keine Benutzerverwaltungs-UI):

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'deine@adresse.example' AND r."Name" = 'Admin'
ON CONFLICT DO NOTHING;
```

Direkt im laufenden PostgreSQL-Container:

```bash
docker compose exec -T postgres psql -U blogcms -d blogcms -c "INSERT INTO \"AspNetUserRoles\" (\"UserId\", \"RoleId\") SELECT u.\"Id\", r.\"Id\" FROM \"AspNetUsers\" u, \"AspNetRoles\" r WHERE u.\"Email\" = 'deine@adresse.example' AND r.\"Name\" = 'Admin' ON CONFLICT DO NOTHING;"
```

Optional — das denormalisierte Domänenfeld `AspNetUsers."Role"` angleichen
(nur informativ, **nicht** autorisierungsrelevant):

```sql
UPDATE "AspNetUsers" SET "Role" = 'Admin' WHERE "Email" = 'deine@adresse.example';
```

`Moderator` wird analog mit `r."Name" = 'Moderator'` vergeben.

### 3. Neu anmelden

Rollen werden als Claims im Auth-Cookie gespeichert. Nach der Zuweisung daher
**ab- und wieder anmelden** (`/Account/Logout` → `/Account/Login`), damit die
neue Rolle aktiv wird. Danach erscheint in der Navigation der Eintrag
**„Verwaltung"** (nur für `Admin`) bzw. **„Moderation"** (für `Moderator`/`Admin`).

## Admin-Status in der Datenbank prüfen

Zeigt E-Mail, Bestätigungsstatus, Domänen-Rolle und Identity-Rolle(n) eines Kontos:

```sql
SELECT u."Email",
       u."EmailConfirmed",
       u."Role" AS domain_role,
       COALESCE(r."Name", '(keine)') AS identity_role
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE u."Email" = 'deine@adresse.example';
```

Als Einzeiler im Container:

```bash
docker compose exec -T postgres psql -U blogcms -d blogcms -c "SELECT u.\"Email\", u.\"EmailConfirmed\", u.\"Role\" AS domain_role, COALESCE(r.\"Name\", '(keine)') AS identity_role FROM \"AspNetUsers\" u LEFT JOIN \"AspNetUserRoles\" ur ON ur.\"UserId\" = u.\"Id\" LEFT JOIN \"AspNetRoles\" r ON r.\"Id\" = ur.\"RoleId\" WHERE u.\"Email\" = 'deine@adresse.example';"
```

> **Beispielergebnis** für `bernd.von.platen@gmail.com`:
>
> | Email | EmailConfirmed | domain_role | identity_role |
> |---|---|---|---|
> | bernd.von.platen@gmail.com | t | Reader | Reader |
> | bernd.von.platen@gmail.com | t | Reader | **Admin** |
>
> Interpretation: Das Konto **ist Admin** (Identity-Rolle `Admin`). Das Feld
> `domain_role` steht hier noch auf `Reader` — das ist eine reine
> Informations-Inkonsistenz und beeinflusst die Rechte nicht. Ein Konto kann
> mehrere Identity-Rollen gleichzeitig haben (hier `Reader` und `Admin`).

### Weitere nützliche Abfragen

```sql
-- Alle vorhandenen Rollen
SELECT "Name" FROM "AspNetRoles" ORDER BY "Name";

-- Alle Benutzer mit ihren Rollen
SELECT u."Email", COALESCE(r."Name", '(keine)') AS identity_role
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
ORDER BY u."Email";
```

## Rollen wieder entziehen

```sql
DELETE FROM "AspNetUserRoles" ur
USING "AspNetUsers" u, "AspNetRoles" r
WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  AND u."Email" = 'deine@adresse.example' AND r."Name" = 'Admin';
```

Nach Änderungen gilt ebenfalls: **neu anmelden**, damit die Claims aktualisiert
werden.

## Hinweis zur Spezifikation

Das Pflichtenheft nennt unter der Rolle `Admin` auch „Nutzerverwaltung"
(siehe [`05-Benutzerverwaltung-Auth.md`](../05-Benutzerverwaltung-Auth.md)). Eine
solche UI ist im aktuellen Stand **nicht implementiert**; Rollen werden derzeit
ausschließlich per SQL verwaltet.

## Weiterführend

- Admin-Bereich verwenden: [`02-admin-artikelverwaltung.md`](02-admin-artikelverwaltung.md)
- Blog aus Nutzersicht: [`03-blog-nutzen.md`](03-blog-nutzen.md)
