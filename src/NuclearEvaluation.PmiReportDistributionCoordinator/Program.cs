using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using NuclearEvaluation.Kernel.Data.Context;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;
using NuclearEvaluation.PmiReportDistributionCoordinator.Services;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;
using NuclearEvaluation.PmiReportDistributionCoordinator.Jobs;
using NuclearEvaluation.PmiReportDistributionCoordinator.Consumers;
using NuclearEvaluation.PmiReportDistributionCoordinator.Dispatchers;

namespace NuclearEvaluation.PmiReportDistributionCoordinator;

internal class Program
{
    private const string LogTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}";

    public static void Main(string[] args)
    {
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Grayscale, outputTemplate: LogTemplate)
                .WriteTo.File(path: "logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, outputTemplate: LogTemplate)
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
                    .UseSqlServerStorage(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"), new SqlServerStorageOptions
                    {
                        SchemaName = builder.Configuration["HangfireSettings:DbSchemaName"],
                    });
            });
            builder.Services.AddHangfireServer();

            builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"));
            }, ServiceLifetime.Scoped);

            builder.Services.AddMassTransit((busConfigurator) =>
            {
                busConfigurator.AddConsumer<PmiReportDistributionReplyMessageConsumer>();

                busConfigurator.UsingRabbitMq((IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator rabbitMqConfigurator) =>
                {
                    string hostName = builder.Configuration["RabbitMQSettings:HostName"]!;
                    string userName = builder.Configuration["RabbitMQSettings:UserName"]!;
                    string password = builder.Configuration["RabbitMQSettings:Password"]!;
                    string virtualHost = builder.Configuration["RabbitMQSettings:VirtualHost"]!;
                    string port = builder.Configuration["RabbitMQSettings:Port"]!;
                    Log.Information("Configuring RabbitMQ host: {HostName}, port: {Port}, virtualHost: {VirtualHost}", hostName, port, virtualHost);
                    string uriString = $"amqps://{hostName}:{port}/{virtualHost}";

                    try
                    {
                        rabbitMqConfigurator.Host(new Uri(uriString), hostConfigurator =>
                        {
                            hostConfigurator.Username(userName);
                            hostConfigurator.Password(password);
                        });
                        Log.Information("RabbitMQ host configured successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error configuring RabbitMQ host.");
                        throw;
                    }

                    string queueName = builder.Configuration["PmiReportDistributionSettings:ReplyQueueName"]!;
                    rabbitMqConfigurator.ReceiveEndpoint(queueName, endpointConfigurator =>
                    {
                        endpointConfigurator.ConfigureConsumer<PmiReportDistributionReplyMessageConsumer>(context);
                    });
                });
            });

            builder.Services.AddTransient<IEnqueuePmiReportForPublishingJob, EnqueuePmiReportForPublishingJob>();
            builder.Services.AddTransient<IPmiReportDistributionMessageDispatcher, PmiReportDistributionMessageDispatcher>();
            builder.Services.AddTransient<IPmiReportDistributionService, PmiReportDistributionService>();

            WebApplication app = builder.Build();

            app.MapHangfireDashboard();

            JobScheduler.RegisterJobs();

            Log.Information("Application starting.");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}