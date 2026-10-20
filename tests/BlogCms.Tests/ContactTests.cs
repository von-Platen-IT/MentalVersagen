using System.ComponentModel.DataAnnotations;
using BlogCms.Infrastructure.Contact;
using BlogCms.Web.Pages;
using BlogCms.Web.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class ContactOptionsTests
{
    [Fact]
    public void Defaults_TargetTheOperatorAddress()
    {
        var options = new ContactOptions();

        Assert.Equal("bernd@mentalversagen.de", options.ToAddress);
        Assert.Equal(3, options.MaxPerWindow);
        Assert.Equal(10, options.WindowMinutes);
    }

    [Fact]
    public void SectionName_IsContact()
    {
        Assert.Equal("Contact", ContactOptions.SectionName);
    }
}

public class ContactThrottleTests
{
    private static MemoryCacheContactThrottle Create(int maxPerWindow = 2)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new MemoryCacheContactThrottle(
            cache,
            Options.Create(new ContactOptions
            {
                MaxPerWindow = maxPerWindow,
                WindowMinutes = 10
            }));
    }

    [Fact]
    public void TryAcquire_AllowsUpToTheLimit_ThenBlocks()
    {
        var throttle = Create(maxPerWindow: 2);

        Assert.True(throttle.TryAcquire("203.0.113.1"));
        Assert.True(throttle.TryAcquire("203.0.113.1"));
        Assert.False(throttle.TryAcquire("203.0.113.1"));
    }

    [Fact]
    public void TryAcquire_TracksClientsSeparately()
    {
        var throttle = Create(maxPerWindow: 1);

        Assert.True(throttle.TryAcquire("203.0.113.1"));
        Assert.False(throttle.TryAcquire("203.0.113.1"));
        Assert.True(throttle.TryAcquire("203.0.113.2"));
    }

    [Fact]
    public void TryAcquire_TreatsMissingClientAsOneBucket()
    {
        var throttle = Create(maxPerWindow: 1);

        Assert.True(throttle.TryAcquire(null));
        Assert.False(throttle.TryAcquire(null));
    }
}

public class ContactInputModelTests
{
    private static KontaktModel.InputModel ValidInput() => new()
    {
        Name = "Erika Beispiel",
        Email = "erika@example.com",
        Subject = "Frage zum Blog",
        Message = "Das ist eine ausreichend lange Nachricht.",
        PrivacyAccepted = true
    };

    private static List<ValidationResult> Validate(KontaktModel.InputModel input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            results,
            validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidInput_HasNoErrors()
    {
        Assert.Empty(Validate(ValidInput()));
    }

    [Fact]
    public void PrivacyCheckbox_MustBeAccepted()
    {
        var input = ValidInput();
        input.PrivacyAccepted = false;

        Assert.Contains(Validate(input), result =>
            result.MemberNames.Contains(nameof(KontaktModel.InputModel.PrivacyAccepted)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("keine-mail")]
    public void Email_MustBeValid(string email)
    {
        var input = ValidInput();
        input.Email = email;

        Assert.Contains(Validate(input), result =>
            result.MemberNames.Contains(nameof(KontaktModel.InputModel.Email)));
    }

    [Fact]
    public void Message_MustHaveMinimumLength()
    {
        var input = ValidInput();
        input.Message = "kurz";

        Assert.Contains(Validate(input), result =>
            result.MemberNames.Contains(nameof(KontaktModel.InputModel.Message)));
    }

    [Fact]
    public void Name_MustHaveMinimumLength()
    {
        var input = ValidInput();
        input.Name = "X";

        Assert.Contains(Validate(input), result =>
            result.MemberNames.Contains(nameof(KontaktModel.InputModel.Name)));
    }
}

public class ContactMessageBuilderTests
{
    [Fact]
    public void BuildSubject_AddsPrefix()
    {
        Assert.Equal("[Kontakt] Frage", ContactMessageBuilder.BuildSubject("Frage"));
    }

    [Fact]
    public void BuildSubject_UsesFallback_WhenEmpty()
    {
        Assert.Equal("[Kontakt] Kontaktanfrage", ContactMessageBuilder.BuildSubject("   "));
    }

    [Fact]
    public void BuildSubject_RemovesLineBreaks()
    {
        // Header injection attempt: the line break must not survive.
        var subject = ContactMessageBuilder.BuildSubject("Frage\r\nBcc: spam@example.com");

        Assert.DoesNotContain("\r", subject);
        Assert.DoesNotContain("\n", subject);
        Assert.StartsWith("[Kontakt] Frage", subject);
    }

    [Fact]
    public void BuildBody_EncodesUserInput()
    {
        var body = ContactMessageBuilder.BuildBody(
            "<b>Name</b>",
            "erika@example.com",
            "<script>alert(1)</script>",
            "Zeile 1\nZeile 2");

        Assert.DoesNotContain("<script>", body);
        Assert.DoesNotContain("<b>Name</b>", body);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("<script>"), body);
        Assert.Contains("Zeile 1<br />Zeile 2", body);
    }
}
