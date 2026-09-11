# 02 — Kommentarfunktion

Referenzierte Entitäten aus `DataSchema.md`: `Comment`, `Report`, `MediaAsset` (OwnerType = Comment), `VideoEmbed` (OwnerType = Comment).

## Funktionale Anforderungen

### Kommentare schreiben
- Nur angemeldete Nutzer (`User`) können kommentieren.
- Ein Kommentar ist an genau einen `Article` gebunden.
- Antworten auf Kommentare über `ParentCommentId` (ein Verschachtelungslevel wird empfohlen — tiefere Threads werden UI-seitig flach dargestellt, um Lesbarkeit zu erhalten).
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

### Bearbeiten & Löschen
- Nutzer können eigene Kommentare innerhalb eines konfigurierbaren Zeitfensters bearbeiten (`EditedAt` wird gesetzt).
- Löschen durch den Verfasser: Soft-Delete (`DeletedAt`), Inhalt wird durch Platzhalter ("Kommentar gelöscht") ersetzt, um Thread-Struktur zu erhalten.

## Akzeptanzkriterien

- [ ] Nicht angemeldete Nutzer sehen Kommentare, aber kein Eingabefeld (nur Login-/Registrierungs-Aufforderung).
- [ ] Ein gelöschter Elternkommentar lässt die Antworten weiterhin sichtbar (Thread bleibt strukturell erhalten).
- [ ] Automatischer Filter markiert Treffer als `Flagged`, veröffentlicht sie nicht automatisch.
- [ ] Rate-Limiting greift nachweisbar bei Überschreitung des konfigurierten Schwellwerts.
- [ ] Meldungen sind für Moderatoren nach Anzahl priorisierbar.
