using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using NuclearEvaluation.HangfireJobs.Jobs;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Models.Settings;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.HangfireJobs.Services;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace NuclearEvaluation.HangfireJobs;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Grayscale,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}")
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}")
        .CreateLogger();

        builder.Services.AddSerilog();

        builder.Configuration.AddJsonFile("rabbitMqSettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile("pmiReportDistributionSettings.json", optional: false, reloadOnChange: true);

        builder.Services.Configure<PmiReportDistributionSettings>(builder.Configuration.GetSection("PmiReportDistributionSettings"));

        builder.Services.AddHangfire(configuration =>
        {
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"),
                    new SqlServerStorageOptions
                    {
                        SchemaName = builder.Configuration["HangfireSettings:DbSchemaName"]
                    }
                );
        });
        builder.Services.AddHangfireServer();

        builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"));
        }, ServiceLifetime.Scoped);

        builder.Services.AddMassTransit((IBusRegistrationConfigurator busConfigurator) =>
        {
            busConfigurator.UsingRabbitMq((IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator rabbitMqConfigurator) =>
            {
                string hostName = builder.Configuration["RabbitMQSettings:HostName"]!;
                string userName = builder.Configuration["RabbitMQSettings:UserName"]!;
                string password = builder.Configuration["RabbitMQSettings:Password"]!;
                string virtualHost = builder.Configuration["RabbitMQSettings:VirtualHost"]!;
                string port = builder.Configuration["RabbitMQSettings:Port"]!;
                string uriString = $"amqps://{hostName}:{port}/{virtualHost}";

                rabbitMqConfigurator.Host(
                    new Uri(uriString),
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(userName);
                        hostConfigurator.Password(password);
                    }
                );
            });
        });

        builder.Services.AddTransient<IEnqueueStemReportForPublishingJob, EnqueueStemReportForPublishingJob>();
        builder.Services.AddTransient<IPmiReportDistributionMessageDispatcher, PmiReportDistributionMessageDispatcher>();
        builder.Services.AddTransient<IPmiReportDistributionService, PmiReportDistributionService>();

        WebApplication app = builder.Build();

        app.MapHangfireDashboard();

        JobScheduler.RegisterJobs();

        app.Run();
    }
}