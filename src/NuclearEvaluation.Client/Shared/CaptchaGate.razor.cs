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
            _error = "Please accept the use of cookies to continue.";
            return;
        }

        _solving = true;
        _error = null;
        StateHasChanged();
        await Task.Yield();

        try
        {
            CaptchaChallenge challenge = await Api.GetCaptchaChallenge();
            long? number = await Task.Run(() => SolveProofOfWork(challenge));

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

    static long? SolveProofOfWork(CaptchaChallenge challenge)
    {
        for (long n = 0; n <= challenge.MaxNumber; n++)
        {
            string candidate = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(challenge.Salt + n))).ToLowerInvariant();

            if (candidate == challenge.Challenge)
            {
                return n;
            }
        }

        return null;
    }
}
