# Lizenzen und Paketregeln

**Stand:** 2026-10-18
**Verbindliche Regel:** siehe „Paket- und Lizenzregeln" in [`README.md`](../README.md).

Dieses Dokument ist das **Lizenz-Inventar** des Projekts. Jede neue Abhängigkeit
(NuGet oder LibMan) wird hier mit Lizenz eingetragen, bevor sie verwendet wird.

---

## 1. Regel in Kurzform

- **Nur freie Lizenzen.** Zulässig: MIT, Apache-2.0, BSD-2/3-Clause, ISC,
  PostgreSQL License, Zlib, SIL OFL 1.1 (Schriften), MPL-2.0 (Einzelfallprüfung).
- **Nicht zulässig:** kommerzielle, proprietäre oder „source-available"-Lizenzen
  mit Nutzungsbeschränkungen — z. B. Six Labors Split License, SSPL, BUSL,
  Commons Clause, Elastic License, „Non-Commercial"-Lizenzen sowie Lizenzen,
  die ab einer Umsatz-/Mitarbeitergrenze kostenpflichtig werden.
- **Transitive Abhängigkeiten zählen mit.** Ein freies Paket, das ein
  proprietäres Paket nachzieht, ist nicht zulässig.
- **Prüfen, eintragen, dann verwenden** — in dieser Reihenfolge.

### Prüfbefehle

```bash
# Alle direkten und transitiven Pakete
dotnet list src/BlogCms.slnx package --include-transitive

# Sicherheitslücken
dotnet list src/BlogCms.slnx package --vulnerable --include-transitive

# Client-Bibliotheken (jsDelivr via LibMan)
cat src/BlogCms.Web/libman.json
```

Die Lizenz eines NuGet-Pakets steht auf nuget.org unter „License" bzw. im
Paket selbst (`LICENSE`/`.nuspec`). Bei transitiven Paketen genügt die Prüfung
der in `obj/project.assets.json` gelisteten Bibliotheken.

---

## 2. Direkte NuGet-Pakete

### BlogCms.Domain

| Paket | Version | Lizenz | Status |
|---|---|---|---|
| Microsoft.Extensions.Identity.Stores | 10.0.12 | MIT | ✅ frei |

### BlogCms.Infrastructure

| Paket | Version | Lizenz | Status |
|---|---|---|---|
| AWSSDK.S3 | 4.0.103.1 | Apache-2.0 | ✅ frei |
| HtmlSanitizer | 9.2.1039 | MIT | ✅ frei |
| Magick.NET-Q8-AnyCPU | 15.0.0 | Apache-2.0 | ✅ frei |
| MailKit | 4.17.0 | MIT | ✅ frei |
| Markdig | 1.3.2 | BSD-2-Clause | ✅ frei |
| MimeKit | 4.17.0 | MIT | ✅ frei |
| Microsoft.AspNetCore.DataProtection | 10.0.12 | MIT | ✅ frei |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | MIT | ✅ frei |
| Microsoft.EntityFrameworkCore | 10.0.12 | MIT | ✅ frei |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | MIT | ✅ frei |
| Microsoft.Extensions.Caching.Memory | 10.0.12 | MIT | ✅ frei |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | MIT | ✅ frei |
| Microsoft.Extensions.Http | 10.0.12 | MIT | ✅ frei |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL License | ✅ frei |
| System.Security.Cryptography.Pkcs | 10.0.13 | MIT | ✅ frei |
| System.Security.Cryptography.Xml | 10.0.13 | MIT | ✅ frei |

### BlogCms.Web

| Paket | Version | Lizenz | Status |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | MIT | ✅ frei |
| Microsoft.Web.LibraryManager.Build | 3.0.114 | MIT | ✅ frei |
| Swashbuckle.AspNetCore | 10.3.0 | MIT | ✅ frei |

### BlogCms.Tests

| Paket | Version | Lizenz | Status |
|---|---|---|---|
| coverlet.collector | 6.0.4 | MIT | ✅ frei |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | MIT | ✅ frei |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.12 | MIT | ✅ frei |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | ✅ frei |
| xunit | 2.9.3 | Apache-2.0 | ✅ frei |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | ✅ frei |

---

## 3. Transitive Abhängigkeiten (nicht Microsoft)

Alle übrigen transitiven Pakete stammen aus dem Microsoft-Ökosystem und stehen
unter MIT. Die folgenden Nicht-Microsoft-Pakete werden mitgezogen:

| Paket | Version | Lizenz | Eingebracht durch | Status |
|---|---|---|---|---|
| AngleSharp | 1.7.2 | MIT | HtmlSanitizer | ✅ frei |
| AngleSharp.Css | 1.0.2 | MIT | HtmlSanitizer | ✅ frei |
| AWSSDK.Core | 4.0.102.4 | Apache-2.0 | AWSSDK.S3 | ✅ frei |
| BouncyCastle.Cryptography | 2.6.2 | MIT | MailKit/MimeKit | ✅ frei |
| Humanizer.Core | 2.14.1 | MIT | EF Core Design | ✅ frei |
| Magick.NET.Core | 15.0.0 | Apache-2.0 | Magick.NET-Q8-AnyCPU | ✅ frei |
| Mono.TextTemplating | 3.0.0 | MIT | EF Core Design | ✅ frei |
| Newtonsoft.Json | 13.0.4 | MIT | Swashbuckle | ✅ frei |
| Npgsql | 10.0.3 | PostgreSQL License | Npgsql.EFCore | ✅ frei |
| Microsoft.OpenApi | 2.7.5 | MIT | Swashbuckle | ✅ frei |
| Microsoft.VisualStudio.SolutionPersistence | 1.0.52 | MIT | EF Core Design | ✅ frei |
| Microsoft.Build.Framework | 18.0.2 | MIT | EF Core Design | ✅ frei |
| Microsoft.CodeAnalysis.* | 5.0.0 | MIT | EF Core Design | ✅ frei |
| Microsoft.Extensions.ApiDescription.Server | 10.0.0 | MIT | Swashbuckle | ✅ frei |
| Microsoft.Extensions.DependencyModel | 10.0.12 | MIT | EF Core Design | ✅ frei |
| System.CodeDom | 6.0.0 | MIT | EF Core Design | ✅ frei |
| System.Composition.* | 9.0.0 | MIT | EF Core Design | ✅ frei |
| Swashbuckle.AspNetCore.Swagger / .SwaggerGen / .SwaggerUI | 10.3.0 | MIT | Swashbuckle.AspNetCore | ✅ frei |

**Native Bibliothek:** `Magick.NET-Q8-AnyCPU` bringt die native
ImageMagick-Bibliothek mit. Diese steht unter der **ImageMagick License**
(Apache-2.0-kompatibel, frei) — keine zusätzliche System-Installation nötig.

---

## 4. Client-Bibliotheken (LibMan / jsDelivr)

Quelle: [`src/BlogCms.Web/libman.json`](../src/BlogCms.Web/libman.json)

| Bibliothek | Version | Lizenz | Status |
|---|---|---|---|
| bootstrap | 5.3.3 | MIT | ✅ frei |
| easymde | 2.18.0 | MIT | ✅ frei |
| font-awesome | 4.7.0 | MIT (Code) + SIL OFL 1.1 (Schriften) | ✅ frei |

**Hinweise:**

- `easymde` bündelt CodeMirror (MIT) und marked (MIT) — beide frei.
- **Font Awesome:** 4.7.0 ist die letzte vollständig freie Version. Ab v5 gibt
  es kostenpflichtige „Pro"-Inhalte — bei einem Upgrade **nur** die
  „Free"-Variante verwenden und die Lizenz hier neu eintragen.

---

## 5. Historie der Lizenzprüfungen

| Datum | Änderung |
|---|---|
| 2026-10-18 | `SixLabors.ImageSharp` 4.1.3 (Six Labors Split License, kommerzialisiert) entfernt und durch `Magick.NET-Q8-AnyCPU` 15.0.0 (Apache-2.0) ersetzt. Details: [`plans/imagesharp-ersetzen.md`](../plans/imagesharp-ersetzen.md). |
| 2026-10-18 | Vollständige Bestandsprüfung: alle direkten, transitiven und Client-Abhängigkeiten sind frei lizenziert. Keine proprietären Pakete gefunden. |

---

## 6. Ergebnis der Bestandsprüfung

**Es sind keine proprietären Pakete im Projekt.** Alle Abhängigkeiten stehen
unter MIT, Apache-2.0, BSD-2-Clause, PostgreSQL License oder SIL OFL 1.1.
`dotnet list package --vulnerable --include-transitive` meldet für alle vier
Projekte keine anfälligen Pakete.
