# Blog nutzen (rudimentär)

Kurze Übersicht über die Nutzung des Blogs aus **Leser- und Autorensicht**.
Für das Schreiben/Verwalten von Artikeln siehe
[`02-admin-artikelverwaltung.md`](02-admin-artikelverwaltung.md).

## Grundprinzip

- Artikel werden in **Markdown** verfasst und beim Ausliefern serverseitig
  gerendert und bereinigt (Markdig + HtmlSanitizer).
- Jeder Artikel hat ein **Titelbild**, eine **Kurzbeschreibung**, eine
  **Kategorie** (Politik, Satire, Verschwörungstheorien), optionale **Tags** und
  **Hashtags** sowie einen **Slug** für die URL.
- Nur `Published`-Artikel sind öffentlich sichtbar; geplante Beiträge werden
  automatisch zum festgelegten Zeitpunkt veröffentlicht.
- Artikel besitzen eine **Zugriffsstufe**: `Public` (alle), `Registered` (nur
  angemeldet) oder `Premium` (aktive Berechtigung/Abo; Admin ausgenommen).

## Artikel lesen

| Ziel | URL |
|---|---|
| Startseite | `/` |
| Artikelliste | `/Articles` |
| Einzelner Artikel | `/Articles/{slug}` |
| RSS-Feed (RSS 2.0) | `/feed` |

### Artikelliste (`/Articles`)

- Zeigt die veröffentlichten Artikel, **10 pro Seite**, sortiert nach
  Veröffentlichungsdatum (neueste zuerst; umschaltbar).
- **Freitextsuche:** `/Articles?query={suchbegriff}`
- Filter nach **Kategorie**: `/Articles?category=Satire`
- Filter nach **Tag**: `/Articles?tag={tag-slug}`
- Filter nach **Hashtag**: `/Articles?hashtag={hashtag-slug}`
- Filter nach **Autor**, **Zugriffsstufe** und **Veröffentlichungsdatum**.
- **Sortierung** u. a. nach Datum und Bewertung.
- Seitenwechsel über `?pageNumber=2` usw.

### Artikel-Detailseite (`/Articles/{slug}`)

- Zeigt **Titelbild**, Titel, Autor, Kategorie-Badge, Kurzbeschreibung, Inhalt
  und – falls vorhanden – Bilder und Video-Einbettungen.
- Für `Satire` und `Verschwörungstheorien` erscheint ein Hinweistext
  (Disclaimer).
- **Bewertung:** Angemeldete Nutzer können den Beitrag mit 👍/👎 bewerten
  (änderbar, max. eine Bewertung gleichzeitig); die Summe wird angezeigt.
- Bei `Registered`/`Premium` sehen nicht berechtigte Besucher nur den Teaser
  plus passenden CTA (Anmelden bzw. Mitglied werden).
- Unter dem Artikel befindet sich der **Kommentarbereich** (siehe unten).

### Linklisten (`/LinkLists/{slug}`)

- Redaktionell zusammengestellte, **geordnete** Listen von Beiträgen mit
  anklickbaren Verweisen. Eine Linkliste kann in andere Seiten eingebettet werden.

### RSS-Feed (`/feed`)

- RSS 2.0 über die letzten bis zu **50** veröffentlichten Artikel.
- Einträge enthalten Titel, Link, Kategorie, Datum und den Teaser/Excerpt.
- Als `<link>`/GUID wird `/Articles/{slug}` verwendet.

## Kommentare

Kommentieren erfordert ein **angemeldetes Konto mit bestätigter E-Mail-Adresse**.

- **Hinzufügen:** Auf der Artikeldetailseite (optional mit Bild-Upload).
- **Bearbeiten:** Eigene Kommentare innerhalb des konfigurierten Bearbeitungs-
  fensters (`EditWindowMinutes`).
- **Löschen:** Eigene Kommentare; Moderatoren/Admins können jeden Kommentar
  löschen (Soft-Delete).
- **Bewerten:** Kommentare und Antworten können mit 👍/👎 bewertet werden
  (änderbar, max. eine Bewertung gleichzeitig).
- **Melden:** Kommentare können mit Grund und optionaler Notiz gemeldet werden.
- **Auszeichnen:** Admins (und Autoren für eigene Beiträge) können Kommentare
  hervorheben.

Moderation (Rollen `Moderator`/`Admin`) erfolgt unter `/Moderation`:
offene Meldungen sowie ausstehende/geflaggte Kommentare können dort
**genehmigt**, **abgelehnt** oder Meldungen **verworfen** werden.

> Konfiguration der Kommentare (Modus, Rate-Limit, Bearbeitungsfenster) erfolgt
> über die `Comments`-Sektion in `appsettings.json`.

## Account & Anmeldung

| Aufgabe | URL |
|---|---|
| Registrieren | `/Account/Register` |
| Anmelden / Abmelden | `/Account/Login`, `/Account/Logout` |
| Eigener Account | `/Account/Manage` |
| E-Mail bestätigen | Link aus der Dev-E-Mail unter `bin/…/App_Data/emails/` |

Neue Konten erhalten die Rolle `Reader` und müssen die E-Mail bestätigen,
bevor sie kommentieren, bewerten oder ein Abo abschließen können. Autor-Rechte
(`Author`) sowie `Moderator`/`Admin` werden — wie in
[`01-rollen-und-admin.md`](01-rollen-und-admin.md) beschrieben — per Rollenzuweisung
vergeben.

## Mitgliedschaft & Premium

| Aufgabe | URL |
|---|---|
| Mitgliedschaft / Übersicht | `/Membership` |
| Checkout | `/Membership/Checkout` |
| Spenden | `/Donate` |

Beiträge mit `AccessLevel = Registered` sind nur für angemeldete Benutzer lesbar;
Beiträge mit `AccessLevel = Premium` nur mit aktivem Abo. Im Dev-Modus simuliert
`FakeStripeService` den Checkout (kein echter Stripe-Key nötig). Die
Zugriffsprüfung basiert auf dem **Abo-Status**, nicht allein auf der Rolle. Admins
haben immer Zugriff.

## Newsletter

| Aufgabe | URL |
|---|---|
| Abonnieren | `/Newsletter/Subscribe` |
| Bestätigen (Double-Opt-in) | Link aus der Bestätigungs-E-Mail |
| Abmelden | `/Newsletter/Unsubscribe` |

Der Newsletter nutzt **Double-Opt-in**; nicht bestätigte Anmeldungen werden nach
`UnconfirmedRetentionDays` Tagen automatisch entfernt.

## Typischer Leser-Flow

```mermaid
flowchart TD
    A["/ - Startseite"] --> B["/Articles - Artikelliste"]
    B --> C{"Kategorie/Tag-Filter?"}
    C -->|ja| D["/Articles?category=... bzw. ?tag=..."]
    C -->|nein| E["Artikel auswählen"]
    D --> E
    E --> F["/Articles/{slug}"]
    F --> G{"Premium-Artikel?"}
    G -->|nein| H["Inhalt lesen + kommentieren"]
    G -->|ja| I["/Membership - Abo abschließen"]
    I --> H
```

## Weiterführend

- Rollen & Admin-Zugang: [`01-rollen-und-admin.md`](01-rollen-und-admin.md)
- Artikel schreiben/verwalten: [`02-admin-artikelverwaltung.md`](02-admin-artikelverwaltung.md)
- Details zu Features: Module 01–05 im Projekt-Root
