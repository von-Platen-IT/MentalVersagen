# Fix: YouTube-Videos erscheinen als schwarzes Rechteck

## Ursache

[`VideoEmbedRenderer.Render()`](../src/BlogCms.Web/Content/VideoEmbedRenderer.cs:15) bettet das
oEmbed-HTML der Plattform über `srcdoc` in ein äußeres sandboxed `<iframe>` ein:

```html
<iframe sandbox="allow-scripts allow-popups" srcdoc="<iframe src='https://www.youtube.com/embed/ID'>"></iframe>
```

Probleme:
1. **Opaque Origin:** Ohne `allow-same-origin` erhält das `srcdoc`-Dokument eine undurchsichtige
   Origin. Das innere YouTube-iframe erbt die Sandbox-Flags und läuft ebenfalls in einer opaque
   Origin. Der YouTube-Player verweigert dort die Wiedergabe → schwarzes Rechteck.
2. **Fehlende Berechtigungen:** Das `allow`-Attribut (autoplay, encrypted-media, picture-in-picture)
   und `allowfullscreen` werden nicht an das innere iframe weitergegeben.

## Lösung

Statt rohem oEmbed-HTML per `srcdoc` wird die Video-ID serverseitig mit einem strikten Regex aus
der Original-URL extrahiert und ein **direktes, vertrauenswürdiges iframe** mit einer fest
konstruierten Embed-URL gebaut:

- YouTube → `https://www.youtube-nocookie.com/embed/{id}`
- Vimeo → `https://player.vimeo.com/video/{id}`
- X/TikTok → nicht sicher iframe-fähig (liefern blockquote+script) → Fallback als Link

Vorteile:
- **Sicher:** Es wird kein nutzergeneriertes HTML mehr injiziert, sondern nur eine validierte
  Video-ID verwendet. Damit entfällt das XSS-Risiko, das ursprünglich die Sandbox motivierte.
- **Funktional:** Das iframe lädt die Plattform direkt mit echter Origin; Wiedergabe funktioniert.
- **Keine DB-Migration nötig:** Die Extraktion erfolgt zur Renderzeit aus `VideoEmbed.OriginalUrl`.

## Arbeitsschritte

1. [`VideoEmbedRenderer.cs`](../src/BlogCms.Web/Content/VideoEmbedRenderer.cs:1) um eine
   `ResolveEmbedUrl(platform, url)`-Methode mit strikter ID-Extraktion erweitern.
2. `Render()` auf ein direktes iframe mit `allow`-Attributen und `allowfullscreen` umstellen;
   Fallback auf Link für nicht einbettbare Plattformen.
3. Unit-Tests für die URL-Extraktion (YouTube watch/youtu.be/shorts/embed, Vimeo, Fallback).
4. Tests ausführen und Build verifizieren.
