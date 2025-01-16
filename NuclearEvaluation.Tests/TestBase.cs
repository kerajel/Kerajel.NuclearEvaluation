using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Library.Helpers;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.Server.Services;
using System.Transactions;

namespace NuclearEvaluation.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly TestContext TestContext = new();
    protected readonly TransactionScope TransactionScope;
    protected readonly NuclearEvaluationServerDbContext DbContext;

    protected readonly TimeSpan DefaultWaitForStateTimeout = TimeSpan.FromSeconds(3);

    protected TestBase()
    {
        TransactionManager.ImplicitDistributedTransactions = true;
        TransactionScope = TransactionProvider.CreateScope(isolationLevel: IsolationLevel.Serializable);

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Tests.json", optional: false)
            .Build();

        ConfigureVoidJS();
        ConfigureLogging();

        TestStartup.ConfigureServices(TestContext.Services, configuration);

        TestContext.Services.AddTransient<IProjectService, ProjectService>();
        TestContext.Services.AddTransient<IApmService, ApmService>();
        TestContext.Services.AddTransient<IParticleService, ParticleService>();
        TestContext.Services.AddTransient<ISubSampleService, SubSampleService>();
        TestContext.Services.AddTransient<ISampleService, SampleService>();
        TestContext.Services.AddTransient<ISeriesService, SeriesService>();
        TestContext.Services.AddTransient<IGenericService, GenericService>();
        TestContext.Services.AddTransient<IChartService, ChartService>();

        DbContext = TestContext.Services.GetRequiredService<NuclearEvaluationServerDbContext>();
    }

    void ConfigureLogging()
    {
        TestContext.Services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Trace);
        });
    }

    void ConfigureVoidJS()
    {
        TestContext.JSInterop.SetupVoid("Radzen.preventArrows", _ => true);
        TestContext.JSInterop.SetupVoid("Radzen.createDatePicker", _ => true);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        TransactionScope.Dispose();
        TestContext.Dispose();
    }
}