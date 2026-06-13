using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using Radzen;

namespace NuclearEvaluation.Client.Tests;

/// <summary>
/// Base for bUnit component tests. The HTTP API is mocked, so tests exercise front-end
/// behaviour only — no database or server is required.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected TestContext TestContext { get; }
    protected INuclearEvaluationApi Api { get; }
    protected TimeSpan DefaultWaitForStateTimeout { get; } = TimeSpan.FromSeconds(5);

    protected TestBase()
    {
        TestContext = new TestContext();
        TestContext.JSInterop.Mode = JSRuntimeMode.Loose;

        Api = Substitute.For<INuclearEvaluationApi>();

        TestContext.Services.AddSingleton(Api);
        TestContext.Services.AddSingleton<ISessionCache, SessionCache>();
        TestContext.Services.AddScoped<ProjectViewValidator>();
        TestContext.Services.AddScoped<PresetFilterValidator>();
        TestContext.Services.AddRadzenComponents();
    }

    public void Dispose()
    {
        TestContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
