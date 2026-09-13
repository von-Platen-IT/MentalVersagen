# DataSchema.md — Single Point of Truth

Dieses Dokument definiert das verbindliche Datenmodell für das CMS. Alle Pflichtenheft-Module (siehe `README.md`) referenzieren ausschließlich die hier definierten Entitäten, Felder und Beziehungen. Änderungen am Datenmodell werden **zuerst hier** vorgenommen, bevor sie in Feature-Dokumenten oder Code umgesetzt werden.

Stand: v1.0 · ASP.NET Core / Entity Framework Core (angenommen)

---

## Konventionen

- Alle Entitäten besitzen `Id` (GUID, Primärschlüssel), `CreatedAt` (UTC), sofern nicht anders angegeben.
- Enums werden als `string`-basierte Enums in der DB gespeichert (lesbarer für Debugging/Migrationen).
- Soft-Delete wird über `DeletedAt` (nullable) realisiert, kein Hard-Delete für nutzergenerierte Inhalte (Kommentare, Artikel).
- Fremdschlüssel-Namenskonvention: `<Entity>Id`.

---

## 1. User

Zentrale Konto-Entität, Basis: ASP.NET Core Identity (`IdentityUser<Guid>` erweitert).

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| Email | string | eindeutig, Login |
| DisplayName | string | öffentlicher Anzeigename |
| PasswordHash | string | von Identity verwaltet |
| Role | enum: `Reader`, `Author`, `Premium`, `Moderator`, `Admin` | steuert Zugriff/Paywall; `Reader` ≙ FeatureFix1-Viewer, `Author` ≙ Autor, `Admin` ≙ Administrator |
| EmailConfirmed | bool | |
| SubscriptionStatus | enum: `None`, `Active`, `PastDue`, `Canceled` | Cache-Feld, Quelle der Wahrheit ist `Subscription` (siehe unten) |
| CreatedAt | DateTime | |
| DeletedAt | DateTime? | Soft-Delete / Account-Löschung |

**Beziehungen:** 1:n zu `Article` (als Autor), `Comment`, `Subscription`, `Donation`, `MediaAsset` (als Uploader), `Report` (als Melder), `Rating` (als Bewerter).

---

## 2. Article

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| Title | string | Pflicht (FeatureFix1 BR-021) |
| Slug | string | eindeutig, URL-Segment; bestehende URLs bleiben stabil |
| TitleImageUrl | string? | Titelbild: externe URL; Fallback ist das erste verwaltete `MediaAsset` des Artikels (BR-022) |
| ContentMarkdown | text | Artikeltext im Markdown-Format |
| Excerpt | string? | Kurzbeschreibung/Teaser; **fachlich Pflicht** validiert (BR-023) |
| Category | enum: `Politik`, `Satire`, `Verschwoerungstheorien` | Pflichtfeld — steuert u. a. Kennzeichnung/Disclaimer-Logik |
| AccessLevel | enum: `Public`, `Registered`, `Premium` | Zugriffsstufe (BR-110/113); ersetzt die alleinige `IsPremium`-Logik |
| IsPremium | bool | **abgeleitet** (nicht persistiert): `AccessLevel == Premium`, Abwärtskompatibilität |
| Status | enum: `Draft`, `Scheduled`, `Published`, `Archived` | Lebenszyklus inkl. geplanter Veröffentlichung (BR-032) |
| ScheduledAt | DateTime? | geplanter Veröffentlichungszeitpunkt (nur bei `Status = Scheduled`) |
| AuthorId | Guid (FK → User) | |
| PublishedAt | DateTime? | |
| UpdatedAt | DateTime | |
| DeletedAt | DateTime? | |

**Beziehungen:** n:m zu `Tag` (über `ArticleTag`), n:m zu `Hashtag` (über `ArticleHashtag`), 1:n zu `Comment`, 1:n zu `MediaAsset`, 1:n zu `VideoEmbed`, 1:n zu `Rating`, 1:n zu `LinkListItem`.

> Hinweis: `Category` ist bewusst ein festes Enum (statt freier Tags), da hierüber später ggf. unterschiedliche rechtliche/redaktionelle Kennzeichnungen (z. B. Satire-Disclaimer) automatisiert gesteuert werden. Siehe `01-Content-Verwaltung.md`. FeatureFix1 erlaubt mehrere Kategorien (BR-025) — diese Anwendung nutzt bewusst genau eine.

> Hinweis Titelbild (BR-022): Ein Beitrag besitzt ein Titelbild entweder als externe `TitleImageUrl` oder als erstes verwaltetes `MediaAsset`. Die „Pflicht" wird in der Formular-/Service-Validierung durchgesetzt, nicht per DB-Constraint (zwei zulässige Quellen).

---

## 3. Tag / ArticleTag

| Tag | Feld | Typ |
|---|---|---|
| | Id | Guid |
| | Name | string, eindeutig |
| | Slug | string, eindeutig |

`ArticleTag` (Join-Tabelle): `ArticleId`, `TagId`.

---

## 3a. Hashtag / ArticleHashtag

Freie Hashtags (BR-026) — ergänzend zu Tags. Die technische Beziehung zu `Tag` ist
nicht festgelegt; Hashtags werden hier als eigene, normalisierte Struktur geführt,
damit sie eigenständig such- und filterbar sind.

| Hashtag | Feld | Typ |
|---|---|---|
| | Id | Guid |
| | Name | string, eindeutig (ohne führendes `#`) |
| | Slug | string, eindeutig |

`ArticleHashtag` (Join-Tabelle): `ArticleId`, `HashtagId`.

---

## 4. Comment

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| ArticleId | Guid (FK → Article) | |
| UserId | Guid (FK → User) | |
| ParentCommentId | Guid? (FK → Comment) | für Thread-Antworten |
| ContentText | text | |
| Status | enum: `Pending`, `Approved`, `Rejected`, `Flagged` | siehe `02-Kommentarfunktion.md` für Moderationslogik |
| IsHighlighted | bool | vom Admin/Autor „ausgezeichnet" (BR-063) |
| CreatedAt | DateTime | |
| EditedAt | DateTime? | |
| DeletedAt | DateTime? | |

**Beziehungen:** 1:n zu `MediaAsset` (Bild-Anhänge), 1:n zu `VideoEmbed`, 1:n zu `Report`, 1:n zu `Rating`.

---

## 5. MediaAsset

Bild-Uploads — sowohl für Artikel als auch für Kommentare nutzbar.

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| OwnerType | enum: `Article`, `Comment` | polymorphe Zuordnung |
| ArticleId | Guid? (FK) | gesetzt, wenn OwnerType = Article |
| CommentId | Guid? (FK) | gesetzt, wenn OwnerType = Comment |
| UploadedByUserId | Guid (FK → User) | |
| StoragePath | string | Pfad/Key im Object Storage (z. B. S3/Blob) |
| MimeType | string | |
| FileSizeBytes | long | |
| Width | int? | |
| Height | int? | |
| AltText | string? | Barrierefreiheit |
| CreatedAt | DateTime | |

> Details zu Storage-Backend, Validierung und Bildverarbeitung: siehe `03-Medien-Upload-und-Embedding.md`.

---

## 6. VideoEmbed

Verknüpfte externe Videos (kein eigenes Hosting).

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| OwnerType | enum: `Article`, `Comment` | |
| ArticleId | Guid? (FK) | |
| CommentId | Guid? (FK) | |
| Platform | enum: `YouTube`, `Vimeo`, `X`, `TikTok`, `Other` | |
| OriginalUrl | string | vom Nutzer eingegebene URL |
| EmbedHtml | text | über oEmbed abgerufener Embed-Code |
| ThumbnailUrl | string? | |
| CreatedAt | DateTime | |

---

## 7. Subscription

Abbildung eines Stripe-Abos (Quelle der Wahrheit für Bezahlstatus).

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid (FK → User) | |
| StripeCustomerId | string | |
| StripeSubscriptionId | string | |
| PlanId | string | Referenz auf Stripe-Preis/Produkt |
| Status | enum: `Active`, `PastDue`, `Canceled`, `Trialing` | gespiegelt aus Stripe-Webhook |
| CurrentPeriodEnd | DateTime | |
| CreatedAt | DateTime | |
| CanceledAt | DateTime? | |

---

## 8. Donation

Einmalige Zahlungen (Stripe Checkout oder PayPal), unabhängig vom Abo.

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid? (FK → User) | nullable — auch anonyme Spenden möglich |
| Amount | decimal | |
| Currency | string | ISO-Code, z. B. `EUR` |
| Provider | enum: `Stripe`, `PayPal` | |
| ProviderTransactionId | string | |
| Status | enum: `Pending`, `Completed`, `Failed`, `Refunded` | |
| CreatedAt | DateTime | |

---

## 9. NewsletterSubscriber

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| Email | string, eindeutig | |
| UserId | Guid? (FK → User) | verknüpft, falls registrierter Nutzer |
| ConfirmedAt | DateTime? | Double-Opt-in |
| UnsubscribedAt | DateTime? | |
| CreatedAt | DateTime | |

---

## 10. Report (Meldung)

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| CommentId | Guid (FK → Comment) | |
| ReportedByUserId | Guid (FK → User) | |
| Reason | enum: `Spam`, `Beleidigung`, `Falschinformation`, `Sonstiges` | |
| Note | string? | Freitext |
| Status | enum: `Open`, `Reviewed`, `Dismissed` | |
| CreatedAt | DateTime | |

---

## 11. Rating (Bewertung)

👍/👎-Bewertungen für Beiträge **und** Kommentare (FeatureFix1 BR-070/071/072).

| Feld | Typ | Beschreibung |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid (FK → User) | bewertender, angemeldeter Nutzer |
| ArticleId | Guid? (FK → Article) | gesetzt bei Beitragsbewertung |
| CommentId | Guid? (FK → Comment) | gesetzt bei Kommentarbewertung |
| Value | enum: `ThumbUp`, `ThumbDown` | Bewertungswert |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime? | Zeitpunkt der letzten Änderung |

**Regeln:** Pro Nutzer und Ziel (Beitrag bzw. Kommentar) existiert höchstens **eine**
Bewertung. Eine Bewertung ist änderbar (`UpdatedAt`). Genau eines der Felder `ArticleId`
oder `CommentId` ist gesetzt. Anonyme Besucher können nicht bewerten.

> Eindeutige Indizes: `(UserId, ArticleId)` und `(UserId, CommentId)`.

---

## 12. LinkList / LinkListItem (redaktionelle Linkliste)

Vom Administrator gepflegte, geordnete Listen von Beiträgen (FeatureFix1 BR-090/091/092).

| LinkList | Feld | Typ |
|---|---|---|
| | Id | Guid |
| | Title | string |
| | Slug | string, eindeutig |
| | Description | string? |
| | CreatedAt | DateTime |

| LinkListItem | Feld | Typ |
|---|---|---|
| | Id | Guid |
| | LinkListId | Guid (FK → LinkList) |
| | ArticleId | Guid (FK → Article) |
| | Position | int — redaktionelle Reihenfolge |

**Regeln:** Die Reihenfolge wird über `Position` bestimmt und ist unabhängig vom
Veröffentlichungsdatum. Eine Linkliste kann öffentlich dargestellt und in andere Seiten
eingebettet werden (anklickbare Verweise auf die Beiträge).

---

## Entity-Relationship-Übersicht (vereinfacht)

```
User 1---n Article
User 1---n Comment
User 1---n Subscription
User 1---n Donation
User 1---n MediaAsset
User 1---n Report (als Melder)
User 1---n Rating

Article n---n Tag (via ArticleTag)
Article n---n Hashtag (via ArticleHashtag)
Article 1---n Comment
Article 1---n MediaAsset
Article 1---n VideoEmbed
Article 1---n Rating
Article 1---n LinkListItem

Comment 1---n Comment (ParentCommentId, self-referencing)
Comment 1---n MediaAsset
Comment 1---n VideoEmbed
Comment 1---n Report
Comment 1---n Rating

LinkList 1---n LinkListItem
```

---

## Änderungsprotokoll

| Version | Datum | Änderung |
|---|---|---|
| v1.0 | Initial | Erstfassung: User, Article, Tag, Comment, MediaAsset, VideoEmbed, Subscription, Donation, NewsletterSubscriber, Report |
| v1.1 | FeatureFix1 | Neue Rolle `Author`; `Article.TitleImageUrl`, `AccessLevel`, `ScheduledAt`, `ArticleStatus.Scheduled`; `Comment.IsHighlighted`; neue Entitäten `Hashtag`/`ArticleHashtag`, `Rating`, `LinkList`/`LinkListItem` |
