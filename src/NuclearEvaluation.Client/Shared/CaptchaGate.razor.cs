using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared;

/// <summary>
/// Gates the application behind a self-hosted proof-of-work captcha on first visit.
/// Once solved, the server sets a long-lived cookie and the puzzle is not shown again.
/// </summary>
public partial class CaptchaGate : ComponentBase
{
    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    bool _checking = true;
    bool _verified;
    bool _solving;
    bool _cookiesAccepted;
    string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            CaptchaStatus status = await Api.GetCaptchaStatus();
            _verified = status.Verified;
        }
        catch
        {
            _verified = false;
        }
        finally
        {
            _checking = false;
        }
    }

    async Task Solve()
    {
        if (!_cookiesAccepted)
        {
            _error = "Please allow the human-check cookie to continue.";
            return;
        }

        _solving = true;
        _error = null;
        StateHasChanged();
        await Task.Yield();

        try
        {
            CaptchaChallenge challenge = await Api.GetCaptchaChallenge();
            long? number = await SolveProofOfWork(challenge);

            if (number is null)
            {
                _error = "Could not solve the challenge. Please try again.";
                return;
            }

            CaptchaSolution solution = new()
            {
                Algorithm = challenge.Algorithm,
                Challenge = challenge.Challenge,
                Number = number.Value,
                Salt = challenge.Salt,
                Signature = challenge.Signature,
            };

            CaptchaStatus result = await Api.VerifyCaptcha(solution);
            _verified = result.Verified;

            if (!_verified)
            {
                _error = "Verification failed. Please try again.";
            }
        }
        catch
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _solving = false;
            StateHasChanged();
        }
    }

    internal static async Task<long?> SolveProofOfWork(
        CaptchaChallenge challenge,
        CancellationToken ct = default
    )
    {
        if (challenge.Algorithm != "SHA-256" || challenge.MaxNumber is < 0 or > 1_000_000)
            return null;

        byte[] expected = Convert.FromHexString(challenge.Challenge);
        byte[] salt = Encoding.UTF8.GetBytes(challenge.Salt);
        byte[] input = new byte[salt.Length + 20];
        salt.CopyTo(input, 0);
        byte[] hash = new byte[32];

        for (long number = 0; number <= challenge.MaxNumber; number++)
        {
            ct.ThrowIfCancellationRequested();
            Utf8Formatter.TryFormat(number, input.AsSpan(salt.Length), out int digits);
            SHA256.HashData(input.AsSpan(0, salt.Length + digits), hash);
            if (hash.AsSpan().SequenceEqual(expected))
                return number;

            // WASM runs on the UI thread. Task.Run does not provide a browser worker;
            // yield to the event loop between chunks so the progress UI stays responsive.
            if (number % 1024 == 0)
                await Task.Delay(1, ct);
        }
        return null;
    }
}
