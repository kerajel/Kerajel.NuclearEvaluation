using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.Captcha;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Shared;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using LinqToDB.EntityFrameworkCore;

internal class Program
{
    const string logTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}";

    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = UploadLimits.MaxStemPreviewFileSizeBytes + (1 * 1024 * 1024);
        });

        builder.Services.AddControllers();

        builder.Services.Configure<SandboxSettings>(builder.Configuration.GetSection("Sandbox"));
        SandboxSettings sandboxSettings = builder.Configuration.GetSection("Sandbox").Get<SandboxSettings>() ?? new SandboxSettings();

        builder.Services.AddRateLimiter(options => RateLimitPolicies.Configure(options, sandboxSettings));

        builder.Services.AddSerilog();

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(theme: AnsiConsoleTheme.Grayscale, outputTemplate: logTemplate)
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3,
                outputTemplate: logTemplate)
            .CreateLogger();

        builder.Services.AddTransient<IProjectService, ProjectService>();
        builder.Services.AddTransient<IApmService, ApmService>();
        builder.Services.AddTransient<IParticleService, ParticleService>();
        builder.Services.AddTransient<ISubSampleService, SubSampleService>();
        builder.Services.AddTransient<ISampleService, SampleService>();
        builder.Services.AddTransient<ISeriesService, SeriesService>();
        builder.Services.AddTransient<IChartService, ChartService>();
        builder.Services.AddTransient<IGenericDbService, GenericDbService>();
        builder.Services.AddTransient<IStemPreviewEntryService, StemPreviewEntryService>();
        builder.Services.AddTransient<IStemPreviewService, StemPreviewService>();
        builder.Services.AddTransient<IStemPreviewParser, StemPreviewParser>();

        builder.Services.AddTransient<IPmiReportService, PmiReportService>();
        builder.Services.AddTransient<IPmiReportUploadService, PmiReportUploadService>();

        builder.Services.AddScoped<IEfsFileService, EfsFileService>();
        builder.Services.AddSingleton<IGuidProvider, GuidProvider>();

        builder.Services.AddSingleton<IStorageQuotaService, StorageQuotaService>();
        builder.Services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        builder.Services.AddHostedService<SandboxMaintenanceService>();

        builder.Services.Configure<CaptchaSettings>(builder.Configuration.GetSection("Captcha"));
        builder.Services.AddSingleton<ICaptchaService, CaptchaService>();

        string connectionString = builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection")
            ?? throw new InvalidOperationException("Connection string 'NuclearEvaluationServerDbConnection' is not configured.");

        // Transient DbContext: services are short-lived per API request.
        builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(
            options => options.UseSqlServer(connectionString), ServiceLifetime.Transient);
        builder.Services.AddDbContextFactory<NuclearEvaluationServerDbContext>(
            options => options.UseSqlServer(connectionString), ServiceLifetime.Transient);

        LinqToDBForEFTools.Initialize();

        WebApplication app = builder.Build();

        if (app.Configuration.GetValue("Sandbox:SeedOnStartup", true))
        {
            using IServiceScope scope = app.Services.CreateScope();
            IDatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
            await seeder.EnsureCreatedAndSeededAsync();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseMiddleware<CaptchaGateMiddleware>();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        await app.RunAsync();
    }
}
