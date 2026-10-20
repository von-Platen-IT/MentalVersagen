using BlogCms.Web.Configuration;
using BlogCms.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Tests der öffentlichen Basis-URL: Ohne sie zeigen RSS-Feed und E-Mail-Links
/// hinter einem Reverse Proxy auf den internen Host ("http://app:8080").
/// </summary>
public class SiteOptionsTests
{
    [Fact]
    public void SectionName_IsSite()
    {
        Assert.Equal("Site", SiteOptions.SectionName);
    }

    [Fact]
    public void Defaults_NoPublicBaseUrl_ForwardedHeadersOn()
    {
        var options = new SiteOptions();

        Assert.False(options.HasPublicBaseUrl);
        Assert.Equal(string.Empty, options.NormalizedPublicBaseUrl);
        Assert.True(options.UseForwardedHeaders);
        Assert.Empty(options.KnownProxies);
    }

    [Theory]
    [InlineData("https://mentalversagen.de", "https://mentalversagen.de")]
    [InlineData("https://mentalversagen.de/", "https://mentalversagen.de")]
    [InlineData("  https://mentalversagen.de  ", "https://mentalversagen.de")]
    [InlineData("http://localhost:5041", "http://localhost:5041")]
    [InlineData("https://mentalversagen.de/feed", "https://mentalversagen.de")]
    public void NormalizedPublicBaseUrl_KeepsOriginOnly(string input, string expected)
    {
        var options = new SiteOptions { PublicBaseUrl = input };

        Assert.Equal(expected, options.NormalizedPublicBaseUrl);
        Assert.True(options.HasPublicBaseUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("keine-url")]
    [InlineData("ftp://mentalversagen.de")]
    public void NormalizedPublicBaseUrl_EmptyForInvalidValues(string input)
    {
        var options = new SiteOptions { PublicBaseUrl = input };

        Assert.Equal(string.Empty, options.NormalizedPublicBaseUrl);
        Assert.False(options.HasPublicBaseUrl);
    }

    [Fact]
    public void GetPublicBaseUrl_UsesConfiguredPublicBaseUrl()
    {
        var context = CreateContext(new SiteOptions { PublicBaseUrl = "https://mentalversagen.de" });
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("blogcms-app", 8080);

        Assert.Equal("https://mentalversagen.de", context.GetPublicBaseUrl());
    }

    [Fact]
    public void GetPublicBaseUrl_FallsBackToRequestSchemeAndHost()
    {
        var context = CreateContext(new SiteOptions());
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("mentalversagen.de");

        Assert.Equal("https://mentalversagen.de", context.GetPublicBaseUrl());
    }

    [Fact]
    public void GetPublicBaseUrl_KeepsPortOfRequest()
    {
        var context = CreateContext(new SiteOptions());
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5041);

        Assert.Equal("http://localhost:5041", context.GetPublicBaseUrl());
    }

    private static DefaultHttpContext CreateContext(SiteOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        return context;
    }
}
