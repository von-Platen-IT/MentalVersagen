using BlogCms.Web.Configuration;
using Xunit;

namespace BlogCms.Tests;

public class EnvFileLoaderTests
{
    [Fact]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var lines = new[] { "# Kommentar", "", "   ", "Features__LoginEnabled=true" };

        var result = EnvFileLoader.Parse(lines);

        Assert.Single(result);
        Assert.Equal("true", result["Features__LoginEnabled"]);
    }

    [Fact]
    public void Parse_StripsExportPrefixAndQuotes()
    {
        var lines = new[]
        {
            "export Features__LoginEnabled=\"false\"",
            "Features__MonetizationEnabled='false'"
        };

        var result = EnvFileLoader.Parse(lines);

        Assert.Equal("false", result["Features__LoginEnabled"]);
        Assert.Equal("false", result["Features__MonetizationEnabled"]);
    }

    [Fact]
    public void Parse_KeepsEqualsSignsInValue()
    {
        var lines = new[] { "ConnectionStrings__DefaultConnection=Host=x;Port=5433" };

        var result = EnvFileLoader.Parse(lines);

        Assert.Equal("Host=x;Port=5433", result["ConnectionStrings__DefaultConnection"]);
    }

    [Fact]
    public void Parse_LastValueWins()
    {
        var lines = new[] { "Features__LoginEnabled=true", "Features__LoginEnabled=false" };

        var result = EnvFileLoader.Parse(lines);

        Assert.Equal("false", result["Features__LoginEnabled"]);
    }

    [Fact]
    public void Parse_SkipsLinesWithoutKey()
    {
        var lines = new[] { "=noKey", "noSeparator" };

        var result = EnvFileLoader.Parse(lines);

        Assert.Empty(result);
    }
}
