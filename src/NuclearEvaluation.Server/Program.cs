using System.Reflection;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Server.Controllers;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Server.Services.Captcha;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Server.Services.RemDb;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Server.Services.Workbench;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

internal class Program
{
    const string logTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        bool isDevelopment = builder.Environment.IsDevelopment();
        bool useIsotopeFixtures = builder.Configuration.GetValue<bool>("Sandbox:UseTestFixtures");
        bool assistantEnabled = isDevelopment
            ? builder.Configuration.GetValue<bool>("Assistant:Enabled")
            : builder.Environment.IsProduction()
                && builder.Configuration.GetValue("Assistant:Enabled", true)
                && OpenAiAssistantClient.HasConfiguredKey(builder.Configuration);
        bool localAssistantEnabled = assistantEnabled && isDevelopment;
        if (assistantEnabled)
            builder.Services.AddAssistant(localAssistantEnabled);

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize =
                UploadLimits.MaxStemPreviewFileSizeBytes + (1 * 1024 * 1024);
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();

        IMvcBuilder controllers = builder
            .Services.AddControllers()
            .AddJsonOptions(o =>
                // View models carry navigation back-references (e.g. ProjectView <-> ProjectViewSeriesView).
                o.JsonSerializerOptions.ReferenceHandler = System
                    .Text
                    .Json
                    .Serialization
                    .ReferenceHandler
                    .IgnoreCycles
            );
        if (!useIsotopeFixtures)
        {
            controllers.ConfigureApplicationPartManager(parts =>
                parts.FeatureProviders.Add(new HideFixtureControllers())
            );
        }

        builder.Services.Configure<SandboxSettings>(builder.Configuration.GetSection("Sandbox"));
        SandboxSettings sandboxSettings = builder
            .Configuration.GetSection("Sandbox")
            .Get<SandboxSettings>()
            ?? new SandboxSettings();

        builder.Services.AddRateLimiter(options =>
            RateLimitPolicies.Configure(options, sandboxSettings)
        );

        builder.Services.AddSerilog();

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(theme: AnsiConsoleTheme.Grayscale, outputTemplate: logTemplate)
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3,
                outputTemplate: logTemplate
            )
            .CreateLogger();

        builder.Services.AddSingleton<RadNetSnapshot>();
        builder.Services.AddSingleton<RemDbSnapshot>();
        builder.Services.AddSingleton<WorkbenchArchive>();
        builder.Services.AddSingleton<WorkbenchStore>();
        builder.Services.AddSingleton<WorkbenchQueryEngine>();
        builder.Services.AddSingleton<ImportReviewStore>();
        builder.Services.AddSingleton<ImportReviewSample>();
        if (useIsotopeFixtures)
            AddIsotopeFixtures(builder);

        builder
            .Services.AddOptions<CaptchaSettings>()
            .Bind(builder.Configuration.GetSection("Captcha"))
            .Validate(
                s => s.MaxNumber is >= 1 and <= 1_000_000,
                "Captcha difficulty must be between 1 and 1,000,000."
            )
            .Validate(
                s =>
                    s.ChallengeTtlMinutes is >= 1 and <= 60
                    && s.VerificationTtlDays is >= 1 and <= 365,
                "Captcha lifetimes are outside the supported range."
            )
            .ValidateOnStart();
        builder.Services.AddSingleton<ICaptchaService, CaptchaService>();

        WebApplication app = builder.Build();

        if (
            app.Environment.IsProduction()
            && app.Configuration["ImportReview:LargeSampleDirectory"] is null
        )
        {
            RetiredRadNetCsvCollection.Remove(app.Environment.ContentRootPath, app.Logger);
        }

        if (useIsotopeFixtures && app.Configuration.GetValue("Sandbox:SeedOnStartup", true))
        {
            await ApplyMigrationsWithRetryAsync(app);
        }

        app.UseExceptionHandler();
        if (localAssistantEnabled)
            app.UseLocalAssistantAccess();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseBlazorFrameworkFiles();
        // The entry page and scoped CSS use stable paths. Revalidate them alongside each
        // Blazor build so a cached stylesheet cannot lag behind the component markup.
        StaticFileOptions staticFiles = new()
        {
            OnPrepareResponse = asset =>
            {
                if (asset.File.Name is "index.html" or "NuclearEvaluation.Client.styles.css")
                    asset.Context.Response.Headers.CacheControl = "no-cache";
            },
        };
        app.UseStaticFiles(staticFiles);
        app.UseRouting();
        app.UseRateLimiter();
        if (!app.Environment.IsDevelopment())
            app.UseMiddleware<CaptchaGateMiddleware>();
        app.MapGet("/api/site/features", () => new SiteFeatures(useIsotopeFixtures));
        app.MapControllers();
        if (assistantEnabled)
            app.MapAssistant(localAssistantEnabled);
        if (!localAssistantEnabled)
            app.MapFallback("/mcp/{**path}", () => Results.NotFound());

        // Unmatched API routes must not fall through to the SPA shell. This fallback is more
        // specific than the catch-all file fallback below, so it wins for /api/* only and
        // returns a proper 404 instead of index.html with HTTP 200.
        app.MapFallback("/api/{**slug}", () => Results.NotFound());

        app.MapFallbackToFile("index.html", staticFiles);

        await app.RunAsync();
    }

    static void AddIsotopeFixtures(WebApplicationBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString(
            "NuclearEvaluationServerDbConnection"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'NuclearEvaluationServerDbConnection' is not configured."
            );
        }
        builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(
            options => options.UseSqlServer(connectionString),
            ServiceLifetime.Transient
        );
        builder.Services.AddDbContextFactory<NuclearEvaluationServerDbContext>(
            options => options.UseSqlServer(connectionString),
            ServiceLifetime.Transient
        );
        LinqToDBForEFTools.Initialize();

        builder.Services.AddTransient<IProjectService, ProjectService>();
        builder.Services.AddTransient<IApmService, ApmService>();
        builder.Services.AddTransient<IParticleService, ParticleService>();
        builder.Services.AddTransient<ISubSampleService, SubSampleService>();
        builder.Services.AddTransient<ISampleService, SampleService>();
        builder.Services.AddTransient<ISeriesService, SeriesService>();
        builder.Services.AddTransient<IChartService, ChartService>();
        builder.Services.AddTransient<IGenericDbService, GenericDbService>();
        builder.Services.AddSingleton<IStemSessionManager, StemSessionManager>();
        builder.Services.AddScoped<StemUploadLog>();
        builder.Services.AddTransient<IStemPreviewEntryService, StemPreviewEntryService>();
        builder.Services.AddTransient<IStemPreviewService, StemPreviewService>();
        builder.Services.AddTransient<IStemPreviewParser, StemPreviewParser>();
        builder.Services.AddSingleton<IEfsFileService, EfsFileService>();
        builder.Services.AddSingleton<IGuidProvider, GuidProvider>();
        builder.Services.AddSingleton<IStorageQuotaService, StorageQuotaService>();
        builder.Services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        builder.Services.AddHostedService<StartupSeedService>();
        builder.Services.AddHostedService<SandboxMaintenanceService>();
    }

    sealed class HideFixtureControllers : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            Type[] fixtures =
            [
                typeof(ChartsController),
                typeof(PresetFiltersController),
                typeof(ProjectsController),
                typeof(SeriesController),
                typeof(StemController),
                typeof(ViewsController),
            ];
            foreach (Type fixture in fixtures)
                feature.Controllers.Remove(fixture.GetTypeInfo());
        }
    }

    // SQL Server in docker may still be warming up when the app starts; retry a few times.
    static async Task ApplyMigrationsWithRetryAsync(WebApplication app)
    {
        const int maxAttempts = 10;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using IServiceScope scope = app.Services.CreateScope();
                IDatabaseSeeder seeder =
                    scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
                await seeder.ApplyMigrationsAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Log.Warning(
                    ex,
                    "Database not ready (attempt {Attempt}/{Max}); retrying in 5s.",
                    attempt,
                    maxAttempts
                );
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
