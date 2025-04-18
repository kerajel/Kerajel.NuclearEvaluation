using Radzen;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using LinqToDB.EntityFrameworkCore;

internal class Program
{
    const string logTemlate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}";

    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile("stemSettings.json", optional: false, reloadOnChange: true);
        builder.Services.Configure<StemSettings>(builder.Configuration.GetSection(nameof(StemSettings)));

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
        });

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor().AddHubOptions(o =>
        {
            o.MaximumReceiveMessageSize = 10 * 1024 * 1024;  // 100 MB
        });
        builder.Services.AddRadzenComponents();

        builder.Services.AddRadzenCookieThemeService(options =>
        {
            options.Name = "NuclearEvaluationTheme";
            options.Duration = TimeSpan.FromDays(365);
        });

        builder.Services.AddSerilog();

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Grayscale,
                outputTemplate: logTemlate)
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3,
                outputTemplate: logTemlate)
            .CreateLogger();

        builder.Services.AddScoped<ISessionCache, SessionCache>();

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

        builder.Services.AddScoped<ITempTableService, TempTableService>();
        builder.Services.AddScoped<IEfsFileService, EfsFileService>();

        builder.Services.AddScoped<PresetFilterValidator>();
        builder.Services.AddScoped<ProjectViewValidator>();
        builder.Services.AddScoped<PmiReportSubmissionValidator>();

        builder.Services.AddSingleton<IGuidProvider, GuidProvider>();

        //using Transient registration due to the nature of server-side Blazor
        builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"));
        }, ServiceLifetime.Transient);
        builder.Services.AddDbContextFactory<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"));
        }, ServiceLifetime.Transient);

        LinqToDBForEFTools.Initialize();

        builder.Services.AddHttpClient("NuclearEvaluation.Server").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false }).AddHeaderPropagation(o => o.Headers.Add("Cookie"));
        builder.Services.AddHeaderPropagation(o => o.Headers.Add("Cookie"));
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<SecurityService>();
        builder.Services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<NuclearEvaluationServerDbContext>().AddDefaultTokenProviders();
        builder.Services.AddControllers().AddOData(o =>
        {
            ODataConventionModelBuilder oDataBuilder = new();
            oDataBuilder.EntitySet<ApplicationUser>("ApplicationUsers");
            StructuralTypeConfiguration usersType = oDataBuilder.StructuralTypes.First(x => x.ClrType == typeof(ApplicationUser));
            usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Password)));
            usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.ConfirmPassword)));
            oDataBuilder.EntitySet<ApplicationRole>("ApplicationRoles");
            o.AddRouteComponents("odata/Identity", oDataBuilder.GetEdmModel()).Count().Filter().OrderBy().Expand().Select().SetMaxTop(null).TimeZone = TimeZoneInfo.Utc;
        });
        builder.Services.AddScoped<AuthenticationStateProvider, ApplicationAuthenticationStateProvider>();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        WebApplication app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseHeaderPropagation();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.Run();
    }
}