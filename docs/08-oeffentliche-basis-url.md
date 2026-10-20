# Öffentliche Adresse der Anwendung (RSS-Feed & E-Mail-Links)

Alle absoluten URLs, die nach außen gehen, baue ich auf der **öffentlichen
Basis-URL** auf:

- die RSS-Feed-Adresse auf `/Newsletter/Subscribe` und die Links im Feed selbst,
- die Bestätigungs- und Abmelde-Links im Newsletter-Versand,
- den E-Mail-Bestätigungslink bei der Registrierung,
- den Link zum Zurücksetzen des Passworts,
- die Rückkehr-URLs des Zahlungsanbieters (Checkout).

## Warum das nötig ist

Die Adresse lässt sich nicht fest im Code eintragen, weil dieselbe Anwendung
lokal, in der CI und auf dem Produktionsserver mit unterschiedlichen Adressen
läuft. Sie wird deshalb aus dem HTTP-Request abgeleitet — daraus entstehen
aber zwei Fallen:

1. **Hinter einem Reverse Proxy** (nginx, Traefik, Caddy) sieht die
   Anwendung nur den internen Host (`blogcms-app:8080`) und das Schema `http`.
   Ohne Forwarded-Header zeigen Feed und E-Mails ins Leere.
2. **Bei festem Port-Mapping** (`${APP_PORT}:8080`) entspricht die Adresse des
   Containers nicht der öffentlichen Adresse.

## Zwei Wege, die öffentliche Adresse festzulegen

### 1. Feste Basis-URL (empfohlen für Produktion)

```bash
# .env
Site__PublicBaseUrl=https://mentalversagen.de
```

Die Konfiguration hat Vorrang vor allem anderen. Sie ist unabhängig von
Proxy, Host-Header und Port-Mapping und damit die zuverlässigste Variante.
Es wird nur der Ursprung (Schema + Host + Port) verwendet — Pfade werden
bewusst verworfen.

### 2. Forwarded-Header des Reverse Proxys

```bash
# .env
Site__UseForwardedHeaders=true
```

Standardmäßig aktiv: Die Anwendung wertet `X-Forwarded-Proto`, `X-Forwarded-Host`
und `X-Forwarded-For` aus. Der Proxy muss diese Header setzen, z. B. nginx:

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host              $host;
    proxy_set_header X-Forwarded-Host  $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
}
```

> **Sicherheit:** Solange `Site__KnownProxies` leer ist, wird jedem Proxy
> vertraut — Host-Header und Client-IP sind dann manipulierbar. Das ist in
> einem Container-Netz üblich (die Proxy-IP wechselt). Ist die Anwendung
> zusätzlich direkt aus dem Internet erreichbar, die Proxy-IP(s) eintragen:
>
> ```bash
> Site__KnownProxies=172.18.0.3,172.18.0.4
> ```

## Verhalten im Überblick

| Szenario | Ergebnis (Feed-Adresse) |
|---|---|
| Lokal ohne Proxy, keine Konfiguration | `http://localhost:5080/feed` |
| Hinter Proxy mit `X-Forwarded-*` | `https://mentalversagen.de/feed` |
| `Site__PublicBaseUrl` gesetzt | `https://mentalversagen.de/feed` (gewinnt immer) |

## Technisches

- Konfiguration: [`SiteOptions`](../src/BlogCms.Web/Configuration/SiteOptions.cs) (Sektion `Site`)
- Zentrale Hilfsfunktion: [`HttpContextExtensions.GetPublicBaseUrl()`](../src/BlogCms.Web/Extensions/HttpContextExtensions.cs)
- Middleware: `app.UseForwardedHeaders(...)` in [`Program.cs`](../src/BlogCms.Web/Program.cs) — steht bewusst ganz vorne in der Pipeline, damit auch die HTTPS-Umleitung das korrekte Schema sieht.
- Tests: [`SiteOptionsTests`](../tests/BlogCms.Tests/SiteOptionsTests.cs)

## Schnellcheck im Betrieb

```bash
# Feed-Adresse der Abo-Seite ansehen
curl -s http://localhost:5080/Newsletter/Subscribe | grep -o 'http[^<"]*/feed'

# Feed selbst (self-Link und Item-Links)
curl -s http://localhost:5080/feed | grep -oE '<(link|guid)>[^<]*</(link|guid)>' | head -5
```
