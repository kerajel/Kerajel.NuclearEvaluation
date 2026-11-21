using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Server.Services.Captcha;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public class CaptchaServiceTests
{
    const string Secret = "test-only-key";

    static CaptchaService Create(string secret = Secret) =>
        new(
            Options.Create(new CaptchaSettings { Secret = secret, MaxNumber = 2 }),
            new EphemeralDataProtectionProvider(),
            TimeProvider.System
        );

    static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [Theory]
    [InlineData("")]
    [InlineData(Secret)]
    public void IssuedChallengeAndCookieRoundTrip(string secret)
    {
        CaptchaService service = Create(secret);
        CaptchaChallenge challenge = service.CreateChallenge();
        Assert.True(service.VerifySolution(Solve(challenge)));
        Assert.True(service.IsVerificationTokenValid(service.IssueVerificationToken()));
        Assert.False(
            Create("different key").IsVerificationTokenValid(service.IssueVerificationToken())
        );
    }

    static CaptchaSolution Solve(CaptchaChallenge challenge)
    {
        long number = Enumerable
            .Range(0, challenge.MaxNumber + 1)
            .Single(n =>
                Hash(challenge.Salt + n.ToString(CultureInfo.InvariantCulture))
                == challenge.Challenge
            );
        return new CaptchaSolution
        {
            Salt = challenge.Salt,
            Challenge = challenge.Challenge,
            Signature = challenge.Signature,
            Number = number,
        };
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

    [Theory]
    [InlineData(null)]
    [InlineData(Secret)]
    public void VerificationSurvivesServerRestartUntilThirtyDaysElapse(string? secret)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("captcha-persistence-");
        try
        {
            ManualClock clock = new();
            string token;
            CaptchaSolution solution;
            using (ServiceProvider firstHost = CreateHost(root.FullName, secret, clock))
            {
                ICaptchaService service = firstHost.GetRequiredService<ICaptchaService>();
                solution = Solve(service.CreateChallenge());
                token = service.IssueVerificationToken();
            }

            using ServiceProvider restartedHost = CreateHost(root.FullName, secret, clock);
            ICaptchaService restarted = restartedHost.GetRequiredService<ICaptchaService>();
            Assert.True(restarted.VerifySolution(solution));
            Assert.True(restarted.IsVerificationTokenValid(token));

            clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1));
            Assert.True(restarted.IsVerificationTokenValid(token));

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(restarted.IsVerificationTokenValid(token));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void ConfiguredSecretAcceptsExistingCookies()
    {
        string payload = DateTimeOffset
            .UtcNow.AddDays(30)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);
        string signature = Convert
            .ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(Secret),
                    Encoding.UTF8.GetBytes("pow." + payload)
                )
            )
            .ToLowerInvariant();

        Assert.True(Create().IsVerificationTokenValid($"{payload}.{signature}"));
    }

    [Fact]
    public void IndependentInstallationsUseDifferentKeys()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("captcha-isolation-");
        try
        {
            using ServiceProvider firstHost = CreateHost(root.FullName, null, TimeProvider.System);
            using ServiceProvider otherHost = CreateHost(
                root.CreateSubdirectory("other-site").FullName,
                null,
                TimeProvider.System
            );
            ICaptchaService first = firstHost.GetRequiredService<ICaptchaService>();
            ICaptchaService other = otherHost.GetRequiredService<ICaptchaService>();
            Assert.False(other.IsVerificationTokenValid(first.IssueVerificationToken()));
            Assert.False(other.VerifySolution(Solve(first.CreateChallenge())));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(Secret)]
    public void TokenExpiryCannotBeExtendedWithoutAValidSignature(string secret)
    {
        CaptchaService service = Create(secret);
        string[] parts = service.IssueVerificationToken().Split('.');
        long laterExpiry = long.Parse(parts[0], CultureInfo.InvariantCulture) + 1;
        Assert.False(service.IsVerificationTokenValid($"{laterExpiry}.{parts[1]}"));
    }

    static ServiceProvider CreateHost(string contentRoot, string? secret, TimeProvider clock)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Captcha:Secret"] = secret,
                    ["Captcha:MaxNumber"] = "2",
                }
            )
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddCaptcha(configuration, contentRoot);
        return services.BuildServiceProvider();
    }

    sealed class ManualClock : TimeProvider
    {
        DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    [Theory]
    [InlineData("")]
    [InlineData(Secret)]
    public void NullSignatureIsRejected(string secret)
    {
        CaptchaService service = Create(secret);
        CaptchaSolution solution = Solve(service.CreateChallenge());
        solution.Signature = null!;
        Assert.False(service.VerifySolution(solution));
    }
}
