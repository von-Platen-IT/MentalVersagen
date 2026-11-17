# 13 — Autoren-Handbuch

Diese Anleitung richtet sich an **Autorinnen und Autoren**. Sie beschreibt, wie
Beiträge angelegt, bearbeitet, geplant und veröffentlicht werden. Für
administrative Aufgaben siehe [`12-administration.md`](12-administration.md).

---

## 1. Voraussetzungen

- Ein Benutzerkonto mit der Rolle **`Author`** (oder `Admin`).
- Eine **bestätigte E-Mail-Adresse**.
- Anmeldung unter `/Account/Login`.

Die Rolle vergibt ein Administrator unter `/Admin/Users`. Nach der Zuweisung
einmal ab- und wieder anmelden.

---

## 2. Einstieg

Nach der Anmeldung erscheint in der Navigation **„Verwaltung“**. Dort liegt
**„Aktenverwaltung“** (`/Admin/Articles`).

Die Übersicht zeigt:

- alle Beiträge, die du bearbeiten darfst (`Author`: nur eigene),
- den Status (`Entwurf`, `Geplant`, `Veröffentlicht`, `Archiviert`),
- Kategorie, Autor und Datum,
- Filter nach Status und eine Freitextsuche.

Über **„Neuer Beitrag“** öffnest du das Formular.

---

## 3. Neuen Beitrag anlegen

1. `/Admin/Articles/Create` öffnen.
2. Die Felder ausfüllen (siehe unten).
3. **Entwurf speichern**, **Sofort veröffentlichen** oder mit Status
   `Geplant` und Zukunftsdatum **planen**.
4. Nach dem Speichern erscheint der Beitrag in der Übersicht.

---

## 4. Formularfelder im Detail

### 4.1 Basisdaten

| Feld | Pflicht | Bedeutung |
|---|---|---|
| **Titel** | ja | Aussagekräftiger Titel, max. 300 Zeichen |
| **Slug** | nein | URL-Abschnitt; leer = automatisch aus dem Titel. Bereits veröffentlichte URLs bleiben bei Titeländerung stabil |
| **Kurzbeschreibung / Teaser** | bei Veröffentlichung | Erscheint in Listen, Vorschau und RSS; max. 500 Zeichen |

### 4.2 Einordnung & Themengebiet

| Feld | Bedeutung |
|---|---|
| **Kategorie** | Genau eine dynamische Kategorie. Ohne Auswahl landet der Beitrag unter `undefined` |
| **Zugriffsstufe** | `Public` (alle), `Registered` (nur angemeldet), `Premium` (Mitgliedschaft/Admin-Freigabe) |
| **Ursprung** | `Human` (Mensch, Standard), `Ai` (KI) oder `Hybrid`; erscheint als Icon mit Tooltip in Listen |
| **Status** | `Draft`, `Scheduled`, `Published`, `Archived` |
| **Geplante Veröffentlichung** | Nur bei `Scheduled`; muss in der Zukunft liegen |

### 4.3 Verknüpfungen

| Feld | Bedeutung |
|---|---|
| **Tags** | Freie Schlagwörter, kommagetrennt (z. B. `parteien, wahlen`) |
| **Hashtags** | Themen-Tags für die Startseiten-Suche, mit oder ohne `#` |

### 4.4 Inhalt

- **Inhalt (Markdown):** der eigentliche Beitragstext.
- Der Editor bietet eine Live-Vorschau und eine Toolbar.
- Bilder können per Klick an der Cursorposition eingefügt werden.

### 4.5 Titelbild & externe Medien

| Feld | Bedeutung |
|---|---|
| **Titelbild-URL** | Externe Bild-URL; Alternative zum Upload |
| **Video-URL** | YouTube, Vimeo, X oder TikTok; wird als sicherer Player eingebettet |
| **Titelbild hochladen** | JPEG, PNG, WebP oder GIF; dient als Titelbild, wenn keine URL gesetzt ist |

Ein Beitrag benötigt **immer** eine Titelbild-Quelle: externe URL, Upload oder
ein bereits vorhandenes Bild in der Galerie.

---

## 5. Markdown-Editor

- **Formatierung:** Überschriften, Fett, Kursiv, Listen, Zitate, Code, Links.
- **Vorschau:** zeigt das gerenderte Ergebnis; das endgültige Sanitizing
  passiert serverseitig.
- **Bilder einfügen:** Bild in der Galerie hochladen und anklicken; der
  Markdown-Link wird an der Cursorposition eingefügt.
- **Tastenkürzel:** die üblichen Editor-Shortcuts (z. B. `Strg+B` für fett).

---

## 6. Bilder und Medien

- **Galerie:** mehrere Bilder pro Beitrag möglich.
- **Titelbild:** das erste Bild oder ein explizit gesetztes Cover.
- **Alt-Texte:** im Galerie-Bereich bearbeitbar; wichtig für Barrierefreiheit.
- **Grenzen:** Artikelbilder max. 10 MB, Kommentarbilder max. 4 MB.
- **Verarbeitung:** serverseitig skaliert, optional nach WebP konvertiert und
  ohne EXIF-Daten gespeichert.

---

## 7. Video einbetten

- Unterstützte Plattformen: **YouTube**, **Vimeo**, **X/Twitter**, **TikTok**.
- Die URL wird serverseitig validiert; daraus wird eine feste, sichere
  Embed-URL gebaut.
- Nicht unterstützte URLs werden als einfacher Link dargestellt.
- Ein bereits gesetztes Video kann durch Leeren des Feldes entfernt werden.

---

## 8. Speichern, Veröffentlichen, Planen

| Aktion | Ergebnis |
|---|---|
| **Entwurf speichern** | Status `Draft`; nicht öffentlich sichtbar |
| **Sofort veröffentlichen** | Status `Published`; `PublishedAt` wird gesetzt |
| **Planen** | Status `Scheduled` mit Zukunftsdatum; automatische Veröffentlichung zum Zeitpunkt |

Regeln beim Veröffentlichen:

- Titelbild-Quelle vorhanden,
- Kurzbeschreibung gefüllt,
- Inhalt gefüllt,
- bei `Scheduled`: `ScheduledAt` in der Zukunft.

`PublishedAt` wird nur beim ersten Wechsel auf `Published` gesetzt und bei
späteren Änderungen nicht überschrieben.

---

## 9. Beitrag bearbeiten

1. In `/Admin/Articles` auf **Bearbeiten** klicken.
2. Änderungen vornehmen und speichern.
3. Der Slug bleibt stabil, solange kein neuer Slug eingegeben wird — bestehende
   Links bleiben gültig.

`Author` kann ausschließlich **eigene** Beiträge bearbeiten; `Admin` alle.

---

## 10. Beitrag löschen

- **Löschen** in der Übersicht führt einen **Soft-Delete** aus.
- Der Beitrag bleibt in der Datenbank, verschwindet aber aus allen Listen.
- Ein Wiederherstellen ist über die Oberfläche nicht vorgesehen.

---

## 11. Vorschau

- Entwürfe, geplante und archivierte Beiträge sind nur für Autor und Admin
  sichtbar.
- Die Vorschau öffnet die normale Artikelseite; Vorschau-Aufrufe werden nicht
  als Leseraufrufe gezählt.

---

## 12. Kommentare der eigenen Beiträge moderieren

Auf der Artikelseite kannst du als Autor:

- Kommentare zu **eigenen** Beiträgen löschen,
- Kommentare **auszeichnen** (hervorheben).

Fremde Beiträge sind ausgenommen; dafür ist die Moderation
(`/Moderation`, Rolle `Moderator`/`Admin`) zuständig.

---

## 13. Checkliste vor Veröffentlichung

- [ ] Titel ist aussagekräftig und max. 300 Zeichen lang.
- [ ] Kurzbeschreibung ist gefüllt (max. 500 Zeichen).
- [ ] Kategorie ist gewählt (oder bewusst `undefined`).
- [ ] Zugriffsstufe ist korrekt (`Public`/`Registered`/`Premium`).
- [ ] Ursprung ist gesetzt (`Human`/`Ai`/`Hybrid`).
- [ ] Titelbild ist vorhanden (URL, Upload oder Galerie).
- [ ] Inhalt ist vollständig und in Markdown formatiert.
- [ ] Tags/Hashtags sind sinnvoll gesetzt.
- [ ] Video-URL funktioniert (falls verwendet).
- [ ] Bei Planung: Datum liegt in der Zukunft.
- [ ] Vorschau geprüft.

---

## 14. Häufige Fehler

| Meldung / Problem | Ursache / Lösung |
|---|---|
| „Zum Veröffentlichen müssen Titelbild, Kurzbeschreibung und Inhalt vorhanden sein.“ | Eines der drei Pflichtelemente fehlt |
| „Bitte eine Titelbild-URL angeben oder ein Bild hochladen.“ | Keine Titelbild-Quelle gesetzt |
| „Der geplante Zeitpunkt muss in der Zukunft liegen.“ | `ScheduledAt` liegt in der Vergangenheit |
| Slug wird nicht übernommen | Slug ist bereits vergeben; das System erzeugt einen eindeutigen Suffix |
| Bild wird nicht angezeigt | Upload fehlgeschlagen oder Storage-Pfad nicht erreichbar |
| Video erscheint nur als Link | Plattform wird nicht sicher eingebettet (z. B. unbekannte URL) |
| Beitrag erscheint nicht öffentlich | Status ist nicht `Published` oder `PublishedAt` fehlt |
| „Forbidden“ beim Bearbeiten | Der Beitrag gehört einem anderen Autor |
