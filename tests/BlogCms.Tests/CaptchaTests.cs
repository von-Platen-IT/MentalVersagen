using System.Globalization;
using BlogCms.Infrastructure.Captcha;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogCms.Tests;

public class CaptchaServiceTests
{
    private static DataProtectionCaptchaService CreateService(CaptchaOptions? options = null)
    {
        return new DataProtectionCaptchaService(
            new EphemeralDataProtectionProvider(),
            Options.Create(options ?? new CaptchaOptions { MinimumSeconds = 0 }));
    }

    private static int Solve(string question)
    {
        var parts = question.Split('+');
        Assert.Equal(2, parts.Length);

        return int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture)
             + int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Issue_ReturnsQuestionAndToken()
    {
        var service = CreateService();

        var challenge = service.Issue();

        Assert.Matches(@"^\d+ \+ \d+$", challenge.Question);
        Assert.False(string.IsNullOrWhiteSpace(challenge.Token));
    }

    [Fact]
    public void Issue_ProducesDifferentChallenges()
    {
        var service = CreateService();

        var first = service.Issue();
        var second = service.Issue();

        // Tokens are randomized (salt), so they must never be identical.
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public void Validate_AcceptsCorrectAnswer()
    {
        var service = CreateService();
        var challenge = service.Issue();

        var result = service.Validate(challenge.Token, Solve(challenge.Question).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.Valid, result);
    }

    [Fact]
    public void Validate_RejectsWrongAnswer()
    {
        var service = CreateService();
        var challenge = service.Issue();

        var result = service.Validate(challenge.Token, (Solve(challenge.Question) + 1).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.WrongAnswer, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("keine Zahl")]
    public void Validate_RejectsMissingOrNonNumericAnswer(string? answer)
    {
        var service = CreateService();
        var challenge = service.Issue();

        var result = service.Validate(challenge.Token, answer);

        Assert.Equal(CaptchaResult.WrongAnswer, result);
    }

    [Fact]
    public void Validate_RejectsTooFastSubmission()
    {
        // A minimum of 60 seconds makes an immediate answer "too fast".
        var service = CreateService(new CaptchaOptions { MinimumSeconds = 60 });
        var challenge = service.Issue();

        var result = service.Validate(challenge.Token, Solve(challenge.Question).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.TooFast, result);
    }

    [Fact]
    public void Validate_RejectsExpiredChallenge()
    {
        // A negative TTL makes every challenge already expired.
        var service = CreateService(new CaptchaOptions { MinimumSeconds = 0, TtlMinutes = -1 });
        var challenge = service.Issue();

        var result = service.Validate(challenge.Token, Solve(challenge.Question).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.Expired, result);
    }

    [Fact]
    public void Validate_RejectsTamperedToken()
    {
        var service = CreateService();
        var challenge = service.Issue();

        // Flip the last character of the protected payload.
        var tampered = challenge.Token[..^1] + (challenge.Token[^1] == 'A' ? 'B' : 'A');

        var result = service.Validate(tampered, Solve(challenge.Question).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.InvalidToken, result);
    }

    [Fact]
    public void Validate_RejectsForeignToken()
    {
        var service = CreateService();
        var otherService = new DataProtectionCaptchaService(
            new EphemeralDataProtectionProvider(),
            Options.Create(new CaptchaOptions { MinimumSeconds = 0 }));

        var foreign = otherService.Issue();

        var result = service.Validate(foreign.Token, Solve(foreign.Question).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CaptchaResult.InvalidToken, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_RejectsMissingToken(string? token)
    {
        var service = CreateService();

        var result = service.Validate(token, "7");

        Assert.Equal(CaptchaResult.InvalidToken, result);
    }
}
