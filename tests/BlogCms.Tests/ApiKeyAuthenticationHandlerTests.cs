using System.Text.Encodings.Web;
using BlogCms.Web.Configuration;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogCms.Tests;

/// <summary>
/// Tests der API-Key-Authentifizierung: gültiger Key, falscher Key, fehlender
/// Header, Fail-Closed ohne konfigurierten Key, Autor-Claim und die
/// 401-ProblemDetails-Antwort der Challenge.
/// </summary>
public class ApiKeyAuthenticationHandlerTests
{
    private const string ConfiguredKey = "0123456789abcdef0123456789abcdef";

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static async Task<(ApiKeyAuthenticationHandler Handler, HttpContext Context)> CreateAsync(
        ApiOptions apiOptions, string? providedKey = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (providedKey is not null)
        {
            context.Request.Headers[ApiKeyDefaults.HeaderName] = providedKey;
        }

        var handler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(apiOptions));

        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyDefaults.AuthenticationScheme, null, typeof(ApiKeyAuthenticationHandler)),
            context);

        return (handler, context);
    }

    private static ApiOptions OptionsWithKey(string key, Guid? authorId = null) =>
        new() { Key = key, AuthorId = authorId };

    [Fact]
    public async Task ValidKey_AuthenticatesSuccessfully()
    {
        var (handler, _) = await CreateAsync(OptionsWithKey(ConfiguredKey), ConfiguredKey);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("api", result.Principal?.Identity?.Name);
    }

    [Fact]
    public async Task ValidKey_SetsAuthorIdClaim()
    {
        var authorId = Guid.NewGuid();
        var (handler, _) = await CreateAsync(OptionsWithKey(ConfiguredKey, authorId), ConfiguredKey);

        var result = await handler.AuthenticateAsync();

        var claim = result.Principal?.FindFirst(ApiKeyDefaults.AuthorIdClaimType);
        Assert.NotNull(claim);
        Assert.Equal(authorId.ToString(), claim.Value);
    }

    [Fact]
    public async Task WrongKey_Fails()
    {
        var (handler, _) = await CreateAsync(OptionsWithKey(ConfiguredKey), "ein-vollig-falscher-key");

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MissingHeader_Fails()
    {
        var (handler, _) = await CreateAsync(OptionsWithKey(ConfiguredKey));

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task NoConfiguredKey_FailsEvenWithMatchingHeader()
    {
        // Fail-Closed: ohne konfigurierten Key darf kein Zugriff gelingen,
        // selbst wenn der Client den "richtigen" (leeren) Wert sendet.
        var (handler, _) = await CreateAsync(OptionsWithKey(string.Empty), ConfiguredKey);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Challenge_Returns401ProblemDetails()
    {
        var (handler, context) = await CreateAsync(OptionsWithKey(ConfiguredKey));

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains(ApiKeyDefaults.HeaderName, body);
    }
}
