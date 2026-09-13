# 02 — Kommentarfunktion

Referenzierte Entitäten aus `DataSchema.md`: `Comment`, `Report`, `MediaAsset` (OwnerType = Comment), `VideoEmbed` (OwnerType = Comment).

## Funktionale Anforderungen

Fachliche Referenz: [`FeatureFix1.MD`](FeatureFix1.MD) (Regeln BR-060 bis BR-063, BR-071/072).

### Kommentare schreiben
- Nur angemeldete Nutzer (`User`) können kommentieren; anonyme Besucher sehen nur die
  vorhandenen Kommentare (BR-060).
- Ein Kommentar ist an genau einen `Article` gebunden.
- Antworten auf Kommentare über `ParentCommentId`; die maximale Verschachtelungstiefe wird
  durch die Anwendung bestimmt (UI stellt Threads derzeit flach/auf eine Antwort-Ebene dar).
- Kommentare können Bild-Anhänge (`MediaAsset`) und Video-Verknüpfungen (`VideoEmbed`) enthalten (siehe `03-Medien-Upload-und-Embedding.md` für technische Details).

### Moderation
- Jeder Kommentar hat einen `Status`: `Pending`, `Approved`, `Rejected`, `Flagged`.
- **Moderationsmodus (konfigurierbar):**
  - *Post-Moderation* (empfohlen für Start): Kommentar wird sofort mit `Status = Approved` sichtbar, kann aber gemeldet/nachträglich entfernt werden.
  - *Pre-Moderation* (optional, bei Bedarf aktivierbar): Kommentar startet mit `Status = Pending`, wird erst nach Freigabe durch Rolle `Moderator`/`Admin` sichtbar.
- Automatisierter Basis-Filter (serverseitig) prüft neue Kommentare auf eine konfigurierbare Sperrliste (Spam-Muster, Schimpfwörter) → bei Treffer automatisch `Status = Flagged` statt `Approved`, unabhängig vom gewählten Moderationsmodus.
- Rate-Limiting pro Nutzer (z. B. max. N Kommentare pro Minute) gegen Spam/Flooding.

### Meldefunktion
- Angemeldete Nutzer können einen Kommentar melden → erzeugt `Report`-Eintrag mit `Reason`.
- Moderator-Ansicht listet offene (`Status = Open`) Meldungen, sortiert nach Anzahl Meldungen pro Kommentar.
- Aktionen aus der Moderator-Ansicht: Kommentar freigeben, Kommentar auf `Rejected` setzen (Soft-Delete via `DeletedAt`), Meldung als `Dismissed` markieren.

### Auszeichnen & Autoren-Moderation (BR-063)
- Der `Admin` kann Kommentare **auszeichnen** (`IsHighlighted`) und alle Kommentare
  moderieren/löschen/beantworten.
- Ein `Author` kann Kommentare zu **eigenen** Beiträgen moderieren (löschen, auszeichnen);
  fremde Beiträge sind ausgenommen.

### Bewertung von Kommentaren und Antworten (BR-071/072)
- Angemeldete Nutzer können Kommentare mit 👍/👎 bewerten (`Rating` mit `CommentId`).
- Höchstens eine Bewertung pro Nutzer und Kommentar, änderbar.
- Antworten sind selbst Kommentare und daher ebenfalls bewertbar.

### Bearbeiten & Löschen
- Nutzer können eigene Kommentare innerhalb eines konfigurierbaren Zeitfensters bearbeiten (`EditedAt` wird gesetzt).
- Löschen durch den Verfasser: Soft-Delete (`DeletedAt`), Inhalt wird durch Platzhalter ("Kommentar gelöscht") ersetzt, um Thread-Struktur zu erhalten.

## Akzeptanzkriterien

- [ ] Nicht angemeldete Nutzer sehen Kommentare, aber kein Eingabefeld (nur Login-/Registrierungs-Aufforderung).
- [ ] Ein gelöschter Elternkommentar lässt die Antworten weiterhin sichtbar (Thread bleibt strukturell erhalten).
- [ ] Automatischer Filter markiert Treffer als `Flagged`, veröffentlicht sie nicht automatisch.
- [ ] Rate-Limiting greift nachweisbar bei Überschreitung des konfigurierten Schwellwerts.
- [ ] Meldungen sind für Moderatoren nach Anzahl priorisierbar.
- [ ] Angemeldete Nutzer können Kommentare und Antworten bewerten; Unangemeldete können nicht bewerten.
- [ ] Eine zweite Bewertung durch denselben Nutzer ändert die bestehende Bewertung, statt eine neue anzulegen.
- [ ] `Admin` kann jeden Kommentar auszeichnen; `Author` nur Kommentare der eigenen Beiträge.
