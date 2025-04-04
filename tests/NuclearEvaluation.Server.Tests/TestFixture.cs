using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Server.Interfaces.Data;
using NuclearEvaluation.Server.Interfaces.DB;
using NuclearEvaluation.Server.Interfaces.Evaluation;
using NuclearEvaluation.Server.Services.Data;
using NuclearEvaluation.Server.Services.Db;
using NuclearEvaluation.Server.Services.Evaluation;
using Respawn;

namespace NuclearEvaluation.Server.Tests;

public sealed class TestFixture : IAsyncLifetime
{
    public TestContext TestContext { get; }
    public NuclearEvaluationServerDbContext DbContext { get; }
    public TimeSpan DefaultWaitForStateTimeout { get; } = TimeSpan.FromSeconds(3);

    private Respawner? respawner;
    readonly IConfiguration configuration;

    public TestFixture()
    {
        TestContext = new TestContext();
        configuration = new ConfigurationBuilder()
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
        TestContext.Services.AddTransient<IGenericDbService, GenericDbService>();
        TestContext.Services.AddTransient<IChartService, ChartService>();

        DbContext = TestContext.Services.GetRequiredService<NuclearEvaluationServerDbContext>();
    }

    public async Task InitializeAsync()
    {
        string connString = DbContext.Database.GetConnectionString()!;
        respawner = await Respawner.CreateAsync(connString);
    }

    public async Task DisposeAsync()
    {
        string connectionString = configuration.GetConnectionString("TestDbConnection")!;
        await respawner!.ResetAsync(connectionString);
        TestContext.Dispose();
    }

    private void ConfigureLogging()
    {
        TestContext.Services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Trace);
        });
    }

    private void ConfigureVoidJS()
    {
        TestContext.JSInterop.SetupVoid("Radzen.preventArrows", _ => true);
        TestContext.JSInterop.SetupVoid("Radzen.createDatePicker", _ => true);
    }
}
