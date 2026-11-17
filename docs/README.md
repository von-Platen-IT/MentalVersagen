# MentalVersagen — Dokumentation

Diese Dokumentation beschreibt den **praktischen Umgang** mit dem Blog-CMS:
Rollen & Admin-Zugang, die Artikelverwaltung im Admin-Bereich und die Nutzung
des Blogs aus Leser-/Autorensicht.

Sie ergänzt die bestehenden Dokumente im Projekt-Root:

| Dokument | Inhalt |
|---|---|
| [`RUNNING.md`](../RUNNING.md) | Voraussetzungen, Schnellstart, Client-Bibliotheken (LibMan/Bootstrap), Docker, Konfiguration, Tests |
| [`README.md`](../README.md) | Pflichtenheft-Übersicht, Struktur |
| [`DataSchema.md`](../DataSchema.md) | Single Point of Truth — Datenmodell |
| [`00-Uebersicht.md`](../00-Uebersicht.md) … [`05-Benutzerverwaltung-Auth.md`](../05-Benutzerverwaltung-Auth.md) | Feature-Spezifikationen (Module 01–05) |

## Inhalt dieser Doku

| Datei | Thema |
|---|---|
| [`01-rollen-und-admin.md`](01-rollen-und-admin.md) | Rollenmodell, Admin („Root") werden, Admin-Status in der DB prüfen, Moderator-Zugang |
| [`02-admin-artikelverwaltung.md`](02-admin-artikelverwaltung.md) | Den Admin-Bereich verwenden: Artikel anlegen, bearbeiten, löschen, veröffentlichen |
| [`03-blog-nutzen.md`](03-blog-nutzen.md) | Blog-Nutzung: Lesen, Kategorien/Tags, URL-Filter und Lesezeichen, RSS, Kommentare, Newsletter, Mitgliedschaft |
| [`04-mailversand.md`](04-mailversand.md) | Umsetzungsvorgabe für den echten SMTP-Versand (ersetzt den Dev-Platzhalter, der auf Produktion die Registrierung abbrechen lässt) |
| [`05-management-api.md`](05-management-api.md) | Management-API: Artikel, Hashtags und Kategorien per API verwalten (API-Key, Swagger/OpenAPI, Nutzung mit KI-Tools) |
| [`06-lizenzen-und-pakete.md`](06-lizenzen-und-pakete.md) | Lizenz-Inventar und verbindliche Paketregeln (nur freie Lizenzen) |
| [`07-kontaktformular.md`](07-kontaktformular.md) | Kontaktformular: Konfiguration, Spam-Schutz und Mailversand |
| [`08-oeffentliche-basis-url.md`](08-oeffentliche-basis-url.md) | Öffentliche Adresse der Anwendung: RSS-Feed und E-Mail-Links korrekt hinter Reverse Proxy und in Produktion |
| [`09-mitgliedschaft-stripe.md`](09-mitgliedschaft-stripe.md) | Stripe-Mitgliedschaft als einzige Abo-Quelle: Konfiguration, Stripe-Dashboard, Webhook, Hintergrundjob, Admin-Abo-Übersicht, Fehlerbehebung |
| [`10-abnahme-mitgliedschaft.md`](10-abnahme-mitgliedschaft.md) | Abnahmeliste T01–T15 der Stripe-Mitgliedschaft inkl. Admin-Übersicht, Testergebnissen und offenen Punkten |
| [`11-bezahlfunktionen-erklaert.md`](11-bezahlfunktionen-erklaert.md) | Bezahlfunktionen für Einsteiger: welche Funktionen es gibt, wie Stripe funktioniert, Ablauf, Admin-Abo-Übersicht, Testmodus, Begriffe und Livegang-Checkliste |
| [`12-administration.md`](12-administration.md) | **Administration von Mentalversagen**: Admin-Zugang, alle Verwaltungsseiten, Mitgliedschaften, Einstellungen, Betrieb und Troubleshooting |
| [`13-autoren-handbuch.md`](13-autoren-handbuch.md) | **Autoren-Handbuch**: Anmeldung, Artikel anlegen/bearbeiten/veröffentlichen/planen, Formularfelder, Markdown, Bilder, Videos, Checkliste |
| [`../API_Funktion_Mentalversagen.md`](../API_Funktion_Mentalversagen.md) | **Ausführliche Anleitung zur Management-API** — Zweck, Webserver-Konfiguration, Client-Tools, Skripte (Python/PowerShell), KI-Agenten (Hermes & MCP) |

## Schnellnavigation

| Aufgabe | URL |
|---|---|
| Blog-Start / Artikelliste | `/` bzw. `/Articles` |
| Artikel lesen | `/Articles/{slug}` |
| Linkliste (redaktionell) | `/LinkLists/{slug}` |
| RSS-Feed | `/feed` |
| Registrieren / Anmelden | `/Account/Register`, `/Account/Login` |
| Passwort vergessen / zurücksetzen | `/Account/ForgotPassword` |
| Eigener Account | `/Account/Manage` |
| **Artikelverwaltung (Author/Admin)** | `/Admin/Articles` |
| **Kategorien (Admin)** | `/Admin/Categories` |
| **Linklisten (Admin)** | `/Admin/LinkLists` |
| **Benutzer & Rollen (Admin)** | `/Admin/Users` |
| **Mitgliedschaften (Admin)** | `/Admin/Memberships` |
| **Einstellungen (Admin)** | `/Admin/Settings` |
| **Aktivitätsauswertung (Admin)** | `/Admin/Activity` |
| **Moderation (Moderator/Admin)** | `/Moderation` |
| Mitgliedschaft (Premium) | `/Membership` |
| Spenden | `/Donate` |
| Newsletter | `/Newsletter/Subscribe` |
| Kontakt | `/Kontakt` |
| **Management-API (Artikel per API)** | `/api/v1/articles` (Auth: Header `X-Api-Key`) |
| **Management-API (Kategorien)** | `/api/v1/categories` |
| **Management-API (Hashtags)** | `/api/v1/hashtags` |
| OpenAPI-Spec (für KI-Tools) | `/swagger/v1/swagger.json` |
| Swagger UI (interaktiv) | `/swagger` (in Development bzw. mit `Api__SwaggerUiEnabled=true`) |

> Standard-URL in der lokalen Entwicklung: **http://localhost:5080** (siehe [`RUNNING.md`](../RUNNING.md)).
