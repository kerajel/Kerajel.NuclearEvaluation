using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Server.Services.Captcha;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public class CaptchaServiceTests
{
    const string Secret = "test-only-key";

    static CaptchaService Create(string secret = Secret) =>
        new(Options.Create(new CaptchaSettings { Secret = secret, MaxNumber = 2 }));

    static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [Fact]
    public void IssuedChallengeAndCookieRoundTrip()
    {
        CaptchaService service = Create();
        CaptchaChallenge challenge = service.CreateChallenge();
        long number = Enumerable
            .Range(0, challenge.MaxNumber + 1)
            .Single(n =>
                Hash(challenge.Salt + n.ToString(CultureInfo.InvariantCulture))
                == challenge.Challenge
            );
        Assert.True(
            service.VerifySolution(
                new CaptchaSolution
                {
                    Salt = challenge.Salt,
                    Challenge = challenge.Challenge,
                    Signature = challenge.Signature,
                    Number = number,
                }
            )
        );
        Assert.True(service.IsVerificationTokenValid(service.IssueVerificationToken()));
        Assert.False(
            Create("different key").IsVerificationTokenValid(service.IssueVerificationToken())
        );
    }

    [Theory]
    [InlineData("9223372036854775807.fake")]
    [InlineData("-9223372036854775808.fake")]
    [InlineData("not.a.timestamp")]
    [InlineData("")]
    [InlineData(null)]
    public void MalformedCookiesAreRejectedWithoutThrowing(string? token) =>
        Assert.False(Create().IsVerificationTokenValid(token));

    [Theory]
    [InlineData("salt.9223372036854775807")]
    [InlineData("salt.-9223372036854775808")]
    [InlineData("salt.invalid")]
    public void MalformedSaltIsRejectedWithoutThrowing(string salt) =>
        Assert.False(Create().VerifySolution(new CaptchaSolution { Salt = salt }));

    [Theory]
    [InlineData(-11)]
    [InlineData(1)]
    public void SignedExpiredOrFutureChallengeIsRejected(int minutesFromNow)
    {
        string salt =
            $"salt.{DateTimeOffset.UtcNow.AddMinutes(minutesFromNow).ToUnixTimeSeconds()}";
        string challenge = Hash(salt + "0");
        string signature = Convert
            .ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(Secret),
                    Encoding.UTF8.GetBytes(challenge)
                )
            )
            .ToLowerInvariant();
        Assert.False(
            Create()
                .VerifySolution(
                    new CaptchaSolution
                    {
                        Salt = salt,
                        Challenge = challenge,
                        Signature = signature,
                    }
                )
        );
    }

    [Fact]
    public void MissingConfigurationDoesNotUseASharedSigningKey() =>
        Assert.False(Create("").IsVerificationTokenValid(Create("").IssueVerificationToken()));

    [Fact]
    public void NullSignatureIsRejected()
    {
        CaptchaChallenge challenge = Create().CreateChallenge();
        Assert.False(
            Create()
                .VerifySolution(new CaptchaSolution { Salt = challenge.Salt, Signature = null! })
        );
    }
}
