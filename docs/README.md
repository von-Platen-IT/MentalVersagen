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
| [`03-blog-nutzen.md`](03-blog-nutzen.md) | Rudimentäre Blog-Nutzung: Lesen, Kategorien/Tags, RSS, Kommentare, Newsletter, Mitgliedschaft |

## Schnellnavigation

| Aufgabe | URL |
|---|---|
| Blog-Start / Artikelliste | `/` bzw. `/Articles` |
| Artikel lesen | `/Articles/{slug}` |
| Linkliste (redaktionell) | `/LinkLists/{slug}` |
| RSS-Feed | `/feed` |
| Registrieren / Anmelden | `/Account/Register`, `/Account/Login` |
| Eigener Account | `/Account/Manage` |
| **Artikelverwaltung (Author/Admin)** | `/Admin/Articles` |
| **Linklisten (Admin)** | `/Admin/LinkLists` |
| **Moderation (Moderator/Admin)** | `/Moderation` |
| Mitgliedschaft (Premium) | `/Membership` |
| Spenden | `/Donate` |
| Newsletter | `/Newsletter/Subscribe` |

> Standard-URL in der lokalen Entwicklung: **http://localhost:5080** (siehe [`RUNNING.md`](../RUNNING.md)).
