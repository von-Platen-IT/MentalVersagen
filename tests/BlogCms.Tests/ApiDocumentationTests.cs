using BlogCms.Web.Configuration;
using BlogCms.Web.Controllers;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der Swagger-/OpenAPI-Dokumentation anhand der tatsächlich generierten
/// Spec: ApiKey-Security-Schema, die fünf Endpunkte mit sprechenden operationIds
/// und der Kurzleitfaden für KI-Clients.
/// </summary>
public class ApiDocumentationTests
{
    /// <summary>
    /// Erzeugt die OpenAPI-Spec über dieselbe Konfiguration wie die laufende
    /// Anwendung (ohne HTTP-Host) und liefert sie für die Assertions zurück.
    /// </summary>
    private static OpenApiDocument GenerateDocument()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddControllers()
            .AddApplicationPart(typeof(ManagementArticlesController).Assembly);
        services.AddSwaggerGen(SwaggerSetup.ConfigureGen);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISwaggerProvider>().GetSwagger(SwaggerSetup.DocumentName);
    }

    /// <summary>Minimaler Hosting-Kontext für die Swagger-Generierung im Test.</summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BlogCms.Tests";
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void GeneratedDocument_DefinesApiKeySecuritySchemeInHeader()
    {
        var document = GenerateDocument();

        Assert.NotNull(document.Components);
        Assert.True(document.Components!.SecuritySchemes.ContainsKey(SwaggerSetup.SecuritySchemeName));

        var scheme = document.Components.SecuritySchemes[SwaggerSetup.SecuritySchemeName];
        Assert.Equal(ApiKeyDefaults.HeaderName, scheme.Name);
        Assert.Equal(SecuritySchemeType.ApiKey, scheme.Type);
        Assert.Equal(ParameterLocation.Header, scheme.In);
    }

    [Fact]
    public void GeneratedDocument_ContainsAllManagementEndpointsWithOperationIds()
    {
        var document = GenerateDocument();

        var operationIds = document.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .Select(operation => operation.OperationId)
            .ToList();

        Assert.Contains("listArticles", operationIds);
        Assert.Contains("getArticle", operationIds);
        Assert.Contains("createArticle", operationIds);
        Assert.Contains("updateArticle", operationIds);
        Assert.Contains("deleteArticle", operationIds);
    }

    [Fact]
    public void GeneratedDocument_DescriptionContainsAiGuide()
    {
        var document = GenerateDocument();

        Assert.Equal(SwaggerSetup.Title, document.Info.Title);
        Assert.Contains("X-Api-Key", document.Info.Description);
        Assert.Contains("listArticles", document.Info.Description);
        Assert.Contains("errors[]", document.Info.Description);
    }

    [Fact]
    public void GeneratedDocument_RequiresApiKeyForManagementApi()
    {
        var document = GenerateDocument();

        // Das ApiKey-Requirement gilt entweder global auf Dokument-Ebene oder pro
        // Operation — beides ist gültiges OpenAPI und von KI-Clients auswertbar.
        var globalRequirements = document.Security?.Count ?? 0;
        var securedOperations = document.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .Count(operation => operation.Security is { Count: > 0 });

        Assert.True(globalRequirements > 0 || securedOperations > 0,
            $"Die Spec weist das ApiKey-Requirement nicht aus " +
            $"(global={globalRequirements}, Operationen={securedOperations}).");
    }

    [Fact]
    public void ManagementController_ExposesExactlyFiveActions()
    {
        var actions = typeof(ManagementArticlesController)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(ManagementArticlesController))
            .Where(method => method.IsPublic && !method.IsSpecialName)
            .Select(method => method.Name)
            .ToList();

        Assert.Equal(5, actions.Count);
        Assert.Contains("ListArticles", actions);
        Assert.Contains("GetArticle", actions);
        Assert.Contains("CreateArticle", actions);
        Assert.Contains("UpdateArticle", actions);
        Assert.Contains("DeleteArticle", actions);
    }
}
