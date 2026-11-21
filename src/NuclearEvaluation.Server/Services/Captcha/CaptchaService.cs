using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Captcha;

public class CaptchaSettings
{
    /// <summary>Optional HMAC secret. Without one, use the persisted data protection keys.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Persistent key directory, resolved relative to the application content root.</summary>
    public string KeyDirectory { get; set; } = "App_Data/CaptchaKeys";

    /// <summary>Upper bound of the proof-of-work search space (difficulty).</summary>
    public int MaxNumber { get; set; } = 500_000;

    /// <summary>How long an issued challenge remains solvable.</summary>
    public int ChallengeTtlMinutes { get; set; } = 10;

    /// <summary>How long a successful verification is remembered (cookie lifetime).</summary>
    public int VerificationTtlDays { get; set; } = 30;

    public const string CookieName = "ne_pow";
}

public interface ICaptchaService
{
    CaptchaChallenge CreateChallenge();
    bool VerifySolution(CaptchaSolution solution);
    string IssueVerificationToken();
    bool IsVerificationTokenValid(string? token);
}

/// <summary>
/// Self-hosted ALTCHA-style proof-of-work captcha. No third-party service: the server
/// authenticates challenges and the browser solves a SHA-256 search.
/// </summary>
public class CaptchaService : ICaptchaService
{
    readonly CaptchaSettings _settings;
    readonly byte[]? _secret;
    readonly IDataProtector _signatures;
    readonly TimeProvider _clock;

    public CaptchaService(
        IOptions<CaptchaSettings> settings,
        IDataProtectionProvider dataProtection,
        TimeProvider clock
    )
    {
        _settings = settings.Value;
        _secret = string.IsNullOrWhiteSpace(_settings.Secret)
            ? null
            : Encoding.UTF8.GetBytes(_settings.Secret);
        _signatures = dataProtection.CreateProtector("NuclearEvaluation.Captcha.Signatures.v1");
        _clock = clock;
    }

    public CaptchaChallenge CreateChallenge()
    {
        long issuedAt = _clock.GetUtcNow().ToUnixTimeSeconds();
        string salt = $"{RandomHex(16)}.{issuedAt}";
        int number = RandomNumberGenerator.GetInt32(0, _settings.MaxNumber + 1);

        string challenge = Sha256Hex(salt + number.ToString(CultureInfo.InvariantCulture));
        string signature = Sign(challenge);

        return new CaptchaChallenge
        {
            Algorithm = "SHA-256",
            Challenge = challenge,
            Salt = salt,
            Signature = signature,
            MaxNumber = _settings.MaxNumber,
        };
    }

    public bool VerifySolution(CaptchaSolution solution)
    {
        if (
            solution.Algorithm != "SHA-256"
            || string.IsNullOrEmpty(solution.Salt)
            || solution.Number < 0
            || solution.Number > _settings.MaxNumber
        )
        {
            return false;
        }

        if (!TryGetSaltTimestamp(solution.Salt, out DateTimeOffset issuedAt))
        {
            return false;
        }

        TimeSpan age = _clock.GetUtcNow() - issuedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromMinutes(_settings.ChallengeTtlMinutes))
        {
            return false;
        }

        string expectedChallenge = Sha256Hex(
            solution.Salt + solution.Number.ToString(CultureInfo.InvariantCulture)
        );
        if (!FixedEquals(expectedChallenge, solution.Challenge))
        {
            return false;
        }

        return IsSignatureValid(expectedChallenge, solution.Signature);
    }

    public string IssueVerificationToken()
    {
        long expiry = _clock.GetUtcNow().AddDays(_settings.VerificationTtlDays).ToUnixTimeSeconds();
        string payload = expiry.ToString(CultureInfo.InvariantCulture);
        return $"{payload}.{Sign("pow." + payload)}";
    }

    public bool IsVerificationTokenValid(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string[] parts = token.Split('.');
        if (
            parts.Length != 2
            || !long.TryParse(
                parts[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long expiry
            )
        )
        {
            return false;
        }

        if (
            expiry <= _clock.GetUtcNow().ToUnixTimeSeconds()
            || expiry > DateTimeOffset.MaxValue.ToUnixTimeSeconds()
        )
        {
            return false;
        }

        return IsSignatureValid("pow." + parts[0], parts[1]);
    }

    static bool TryGetSaltTimestamp(string salt, out DateTimeOffset issuedAt)
    {
        issuedAt = default;
        string[] parts = salt.Split('.');
        if (
            parts.Length != 2
            || !long.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long unix
            )
        )
        {
            return false;
        }
        if (
            unix < DateTimeOffset.MinValue.ToUnixTimeSeconds()
            || unix > DateTimeOffset.MaxValue.ToUnixTimeSeconds()
        )
        {
            return false;
        }
        issuedAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        return true;
    }

    static string RandomHex(int bytes) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    string Sign(string value)
    {
        if (_secret is null)
            return _signatures.Protect(value);

        byte[] hash = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    bool IsSignatureValid(string value, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;
        if (_secret is not null)
            return FixedEquals(Sign(value), signature);

        try
        {
            return FixedEquals(value, _signatures.Unprotect(signature));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    static bool FixedEquals(string a, string? b) =>
        b is not null
        && a.Length == b.Length
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b)
        );
}
