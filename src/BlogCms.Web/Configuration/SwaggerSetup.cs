using System.Reflection;
using System.Text.Json;
using BlogCms.Web.Security;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BlogCms.Web.Configuration;

/// <summary>
/// Zentrale Swagger-/OpenAPI-Konfiguration der Management-API. Ausgelagert, damit
/// die Dokumentationseinstellungen sowohl aus <c>Program.cs</c> als auch aus Tests
/// heraus geprüft werden können. Die Spec ist bewusst KI-freundlich: sprechende
/// <c>operationId</c>s, Enums als Klartextwerte und eine Beschreibung mit
/// Kurzleitfaden für automatisierte Clients.
/// </summary>
public static class SwaggerSetup
{
    /// <summary>Name des OpenAPI-Dokuments (Version).</summary>
    public const string DocumentName = "v1";

    /// <summary>Name des Security-Schemes in der Spec.</summary>
    public const string SecuritySchemeName = "ApiKey";

    /// <summary>Titel der API-Dokumentation.</summary>
    public const string Title = "MentalVersagen Blog — Management-API";

    /// <summary>
    /// Beschreibung inklusive Kurzleitfaden für KI-Clients — landet direkt in der
    /// OpenAPI-Spec und wird von Tools wie ChatGPT Actions oder Claude-Connectoren
    /// mitgelesen.
    /// </summary>
    public const string Description = """
        API zur Pflege der Blog-Artikel: anlegen, bearbeiten, löschen und lesen.

        Authentifizierung: Statischer API-Key im Header "X-Api-Key".

        Empfohlener Ablauf für automatisierte Clients:
        1. listArticles — Artikel und ihre IDs/Slugs auflisten (filterbar über status und query).
        2. getArticle — vollständigen Artikel inklusive Markdown-Quelltext lesen.
        3. createArticle — neuen Artikel anlegen; die Antwort enthält die vergebene id und slug.
        4. updateArticle — Artikel vollständig überschreiben (Voll-Update); Statuswechsel
           (Published, Scheduled, Draft, Archived) erfolgen ebenfalls hier.
        5. deleteArticle — Artikel löschen (Soft-Delete: bleibt in der Datenbank erhalten).

        Wichtige Regeln:
        - Für status "Published" oder "Scheduled" sind excerpt, contentMarkdown und eine
          Titelbild-Quelle (titleImageUrl oder uploadedImageIds) Pflicht.
        - Bei status "Scheduled" muss scheduledAt in der Zukunft liegen (ISO-8601, UTC).
        - Validierungsfehler (400) enthalten errors[] mit field und message; die genannten
          Felder korrigieren und die Anfrage erneut senden.
        """;

    /// <summary>
    /// Konfiguriert die OpenAPI-Generierung: Metadaten, ApiKey-Schema,
    /// XML-Kommentare, sprechende <c>operationId</c>s und Enums als Strings.
    /// </summary>
    public static void ConfigureGen(SwaggerGenOptions options)
    {
        options.SwaggerDoc(DocumentName, new OpenApiInfo
        {
            Title = Title,
            Version = DocumentName,
            Description = Description
        });

        options.AddSecurityDefinition(SecuritySchemeName, new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Name = ApiKeyDefaults.HeaderName,
            Type = SecuritySchemeType.ApiKey,
            Description =
                "Statischer API-Key der Installation. Wird als Header '" +
                ApiKeyDefaults.HeaderName + "' mit jedem Request übermittelt."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeName, document)] = []
        });

        // XML-Kommentare einbinden, damit die Beschreibungen der Controller und
        // DTOs in der Spec landen (GenerateDocumentationFile im Web-Projekt).
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Sprechende operationIds in camelCase (createArticle, updateArticle, …) —
        // viele KI-Tools arbeiten damit stabiler als mit generierten Namen.
        options.CustomOperationIds(apiDesc =>
        {
            if (apiDesc.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
                !string.IsNullOrEmpty(action))
            {
                return JsonNamingPolicy.CamelCase.ConvertName(action);
            }

            return null;
        });

        // Enums werden über den JsonStringEnumConverter der MVC-JsonOptions als
        // Klartextwerte (z. B. "Published") abgebildet — konsistent zur Persistenz
        // und gut lesbar für automatisierte Clients.
    }
}
