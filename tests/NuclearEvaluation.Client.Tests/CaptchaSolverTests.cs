using System.Security.Cryptography;
using System.Text;
using NuclearEvaluation.Client.Shared;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class CaptchaSolverTests
{
    [Fact]
    public async Task FindsSolutionAcrossYieldBoundary()
    {
        CaptchaChallenge challenge = new()
        {
            Salt = "test.salt",
            MaxNumber = 2050,
            Challenge = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("test.salt2050"))
            ),
        };
        Assert.Equal(2050L, await CaptchaGate.SolveProofOfWork(challenge));
    }

    [Fact]
    public async Task SolverHonorsCancellation()
    {
        using CancellationTokenSource ct = new();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CaptchaGate.SolveProofOfWork(
                new()
                {
                    MaxNumber = 10,
                    Salt = "salt",
                    Challenge = new string('0', 64),
                },
                ct.Token
            )
        );
    }
}
