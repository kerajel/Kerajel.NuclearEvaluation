using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Captcha;

public class CaptchaSettings
{
    /// <summary>HMAC secret for signing challenges and the verification cookie. Override in production.</summary>
    public string Secret { get; set; } = "change-me-nuclear-evaluation-dev-secret";

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
/// signs challenges with an HMAC secret and the browser solves a SHA-256 search.
/// </summary>
public class CaptchaService : ICaptchaService
{
    readonly CaptchaSettings _settings;
    readonly byte[] _secret;

    public CaptchaService(IOptions<CaptchaSettings> settings)
    {
        _settings = settings.Value;
        _secret = Encoding.UTF8.GetBytes(_settings.Secret);
    }

    public CaptchaChallenge CreateChallenge()
    {
        long issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string salt = $"{RandomHex(16)}.{issuedAt}";
        int number = RandomNumberGenerator.GetInt32(0, _settings.MaxNumber + 1);

        string challenge = Sha256Hex(salt + number.ToString(CultureInfo.InvariantCulture));
        string signature = HmacHex(challenge);

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
        if (solution.Algorithm != "SHA-256" || string.IsNullOrEmpty(solution.Salt))
        {
            return false;
        }

        if (!TryGetSaltTimestamp(solution.Salt, out DateTimeOffset issuedAt))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(_settings.ChallengeTtlMinutes))
        {
            return false;
        }

        string expectedChallenge = Sha256Hex(solution.Salt + solution.Number.ToString(CultureInfo.InvariantCulture));
        if (!FixedEquals(expectedChallenge, solution.Challenge))
        {
            return false;
        }

        string expectedSignature = HmacHex(expectedChallenge);
        return FixedEquals(expectedSignature, solution.Signature);
    }

    public string IssueVerificationToken()
    {
        long expiry = DateTimeOffset.UtcNow.AddDays(_settings.VerificationTtlDays).ToUnixTimeSeconds();
        string payload = expiry.ToString(CultureInfo.InvariantCulture);
        return $"{payload}.{HmacHex("pow." + payload)}";
    }

    public bool IsVerificationTokenValid(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string[] parts = token.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiry))
        {
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiry) < DateTimeOffset.UtcNow)
        {
            return false;
        }

        string expectedSignature = HmacHex("pow." + parts[0]);
        return FixedEquals(expectedSignature, parts[1]);
    }

    static bool TryGetSaltTimestamp(string salt, out DateTimeOffset issuedAt)
    {
        issuedAt = default;
        string[] parts = salt.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix))
        {
            return false;
        }
        issuedAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        return true;
    }

    static string RandomHex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    static string Sha256Hex(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    string HmacHex(string value)
    {
        byte[] hash = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    static bool FixedEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
