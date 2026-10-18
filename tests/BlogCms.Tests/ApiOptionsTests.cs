using BlogCms.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der API-Konfiguration: Options-Verhalten und Bindung aus der
/// Konfigurationssektion "Api" (appsettings / Umgebungsvariablen / .env).
/// </summary>
public class ApiOptionsTests
{
    [Fact]
    public void SectionName_IsApi()
    {
        Assert.Equal("Api", ApiOptions.SectionName);
    }

    [Fact]
    public void IsConfigured_FalseWhenKeyEmpty()
    {
        Assert.False(new ApiOptions().IsConfigured);
        Assert.False(new ApiOptions { Key = "   " }.IsConfigured);
    }

    [Fact]
    public void IsConfigured_TrueWhenKeySet()
    {
        Assert.True(new ApiOptions { Key = "geheimer-key" }.IsConfigured);
    }

    [Fact]
    public void Defaults_RateLimit60AndSwaggerUiDisabled()
    {
        var options = new ApiOptions();

        Assert.Equal(60, options.RateLimitPerMinute);
        Assert.False(options.SwaggerUiEnabled);
        Assert.Null(options.AuthorId);
    }

    [Fact]
    public void BindsFromConfigurationSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Key"] = "test-key-0123456789abcdef",
                ["Api:AuthorId"] = "d3b8f7e0-1111-2222-3333-444455556666",
                ["Api:SwaggerUiEnabled"] = "true",
                ["Api:RateLimitPerMinute"] = "30"
            })
            .Build();

        var options = new ApiOptions();
        config.GetSection(ApiOptions.SectionName).Bind(options);

        Assert.Equal("test-key-0123456789abcdef", options.Key);
        Assert.Equal(Guid.Parse("d3b8f7e0-1111-2222-3333-444455556666"), options.AuthorId);
        Assert.True(options.SwaggerUiEnabled);
        Assert.Equal(30, options.RateLimitPerMinute);
    }
}
