using System.Security.Cryptography;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using NuclearEvaluation.Client.Shared;
using NuclearEvaluation.Shared.Contracts;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Tests;

public class CaptchaGateTests : TestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BusyIndicatorContinuesUntilVerificationCompletes(bool verified)
    {
        TaskCompletionSource<CaptchaChallenge> challenge = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource<CaptchaStatus> verification = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetCaptchaStatus(Arg.Any<CancellationToken>()).Returns(new CaptchaStatus());
        Api.GetCaptchaChallenge(Arg.Any<CancellationToken>()).Returns(challenge.Task);
        Api.VerifyCaptcha(Arg.Any<CaptchaSolution>(), Arg.Any<CancellationToken>())
            .Returns(verification.Task);
        IRenderedComponent<CaptchaGate> gate = TestContext.Render<CaptchaGate>(parameters =>
            parameters.Add(
                component => component.ChildContent,
                "<p id='verified-content'>Application ready</p>"
            )
        );
        IRenderedComponent<RadzenCheckBox<bool>> consent = gate.FindComponent<
            RadzenCheckBox<bool>
        >();
        await consent.InvokeAsync(() => consent.Instance.ValueChanged.InvokeAsync(true));

        Task solve = gate.Find("button[aria-busy]")
            .TriggerEventAsync("onclick", new MouseEventArgs());
        gate.WaitForAssertion(() =>
        {
            Assert.Equal("true", gate.Find("button[aria-busy]").GetAttribute("aria-busy"));
            Assert.True(gate.Find("button[aria-busy]").HasAttribute("disabled"));
            Assert.Contains("saving your verification", gate.Find("[role='status']").TextContent);
        });

        challenge.SetResult(
            new CaptchaChallenge
            {
                Salt = "check",
                MaxNumber = 0,
                Challenge = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("check0"))),
            }
        );
        gate.WaitForAssertion(() =>
            Api.Received(1).VerifyCaptcha(Arg.Any<CaptchaSolution>(), Arg.Any<CancellationToken>())
        );
        Assert.Equal("true", gate.Find("button[aria-busy]").GetAttribute("aria-busy"));
        Assert.True(gate.Find("button[aria-busy]").HasAttribute("disabled"));

        verification.SetResult(new CaptchaStatus { Verified = verified });
        await solve.WaitAsync(DefaultWaitForStateTimeout);

        if (verified)
            gate.WaitForAssertion(() =>
                Assert.Equal("Application ready", gate.Find("#verified-content").TextContent)
            );
        else
            gate.WaitForAssertion(() =>
            {
                Assert.Equal("false", gate.Find("button[aria-busy]").GetAttribute("aria-busy"));
                Assert.False(gate.Find("button[aria-busy]").HasAttribute("disabled"));
            });
    }
}
