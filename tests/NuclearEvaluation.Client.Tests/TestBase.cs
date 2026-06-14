using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;

namespace NuclearEvaluation.Client.Tests;

/// <summary>
/// Base for bUnit component tests. The HTTP API is mocked, so tests exercise front-end
/// behaviour only — no database or server is required.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected BunitContext TestContext { get; }
    protected INuclearEvaluationApi Api { get; }
    protected TimeSpan DefaultWaitForStateTimeout { get; } = TimeSpan.FromSeconds(5);

    protected TestBase()
    {
        TestContext = new BunitContext();
        TestContext.JSInterop.Mode = JSRuntimeMode.Loose;

        Api = Substitute.For<INuclearEvaluationApi>();
        Api.GetSeriesViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<SeriesView>.Succeeded([], 0));
        Api.GetSampleViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<SampleView>.Succeeded([], 0));
        Api.GetSubSampleViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<SubSampleView>.Succeeded([], 0));
        Api.GetApmViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ApmView>.Succeeded([], 0));
        Api.GetParticleViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ParticleView>.Succeeded([], 0));
        Api.GetProjectApmUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        Api.GetProjectParticleUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);

        TestContext.Services.AddSingleton(Api);
        TestContext.Services.AddSingleton<ISessionCache, SessionCache>();
        TestContext.Services.AddSingleton<IGridResultCache, GridResultCache>();
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
