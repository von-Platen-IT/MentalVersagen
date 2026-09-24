using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace BlogCms.Infrastructure.Captcha;

/// <summary>
/// Configuration for the registration captcha (bound from the "Captcha" section).
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    /// <summary>
    /// Minimum time in seconds between issuing and answering a challenge.
    /// Blocks bots that submit the form instantly.
    /// </summary>
    public int MinimumSeconds { get; set; } = 2;

    /// <summary>How long an issued challenge stays valid.</summary>
    public int TtlMinutes { get; set; } = 30;

    /// <summary>Upper bound for the operands of the arithmetic question.</summary>
    public int MaxOperand { get; set; } = 9;
}

/// <summary>Outcome of a captcha validation.</summary>
public enum CaptchaResult
{
    Valid,
    WrongAnswer,
    TooFast,
    Expired,
    InvalidToken
}

/// <summary>A freshly issued challenge: the question shown to the user and its signed token.</summary>
public sealed record CaptchaChallenge(string Question, string Token);

/// <summary>
/// Simple, accessible captcha: an arithmetic question whose expected answer is
/// carried in a tamper-proof (Data Protection) token. No images, no external
/// service, and the check always happens on the server.
/// </summary>
public interface ICaptchaService
{
    CaptchaChallenge Issue();

    CaptchaResult Validate(string? token, string? answer);
}

public sealed class DataProtectionCaptchaService : ICaptchaService
{
    private const string ProtectorPurpose = "BlogCms.Captcha.v1";

    private readonly IDataProtector _protector;
    private readonly CaptchaOptions _options;

    public DataProtectionCaptchaService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<CaptchaOptions> options)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _options = options.Value;
    }

    public CaptchaChallenge Issue()
    {
        var max = Math.Clamp(_options.MaxOperand, 2, 99);
        var left = RandomNumberGenerator.GetInt32(1, max + 1);
        var right = RandomNumberGenerator.GetInt32(1, max + 1);
        var expected = left + right;
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // The token is encrypted and signed, so the expected answer cannot be
        // read or forged by the client.
        var token = _protector.Protect($"{expected}|{issuedAt}");

        return new CaptchaChallenge($"{left} + {right}", token);
    }

    public CaptchaResult Validate(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return CaptchaResult.InvalidToken;
        }

        string payload;
        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            // Tampered or foreign token.
            return CaptchaResult.InvalidToken;
        }

        var parts = payload.Split('|');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected) ||
            !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var issuedAt))
        {
            return CaptchaResult.InvalidToken;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var age = now - issuedAt;

        if (age > _options.TtlMinutes * 60L)
        {
            return CaptchaResult.Expired;
        }

        if (age < _options.MinimumSeconds)
        {
            return CaptchaResult.TooFast;
        }

        if (!int.TryParse(
                (answer ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var given))
        {
            return CaptchaResult.WrongAnswer;
        }

        return given == expected ? CaptchaResult.Valid : CaptchaResult.WrongAnswer;
    }
}
