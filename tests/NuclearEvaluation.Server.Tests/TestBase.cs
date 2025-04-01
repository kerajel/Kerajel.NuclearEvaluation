using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Shared.Services;
using Respawn;
using System.Transactions;

namespace NuclearEvaluation.Server.Tests;

public abstract class TestBase : IAsyncDisposable
{
    protected readonly TestContext TestContext = new();
    protected readonly NuclearEvaluationServerDbContext DbContext;

    protected readonly TimeSpan DefaultWaitForStateTimeout = TimeSpan.FromSeconds(3);

    readonly Respawner respawner;

    protected TestBase()
    {
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

        string connString = DbContext.Database.GetConnectionString()!;
        respawner = Respawner.CreateAsync(connString).Result;
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

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await respawner.ResetAsync("Server=localhost;Connection Timeout=30;Command Timeout=30;Persist Security Info=False;TrustServerCertificate=True;Integrated Security=True;Initial Catalog=NuclearEvaluationServer;MultipleActiveResultSets=True;");
        TestContext.Dispose();
    }
}