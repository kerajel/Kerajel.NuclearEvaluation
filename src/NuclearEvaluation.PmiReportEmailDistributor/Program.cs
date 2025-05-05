using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using RabbitMQ.Client;
using System.Security.Authentication;
using NuclearEvaluation.PmiReportEmailDistributor.Consumers;
using NuclearEvaluation.Messaging.Interfaces;
using NuclearEvaluation.Messaging.Dispatchers;
using NuclearEvaluation.PmiReportEmailDistributor.Settings;

namespace NuclearEvaluation.PmiReportEmailDistributor;

internal class Program
{
    private const string _logTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Exception}{NewLine}{Properties:j}";

    public static void Main(string[] args)
    {
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Grayscale, outputTemplate: _logTemplate)
                .WriteTo.File(path: "logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, outputTemplate: _logTemplate)
                .CreateLogger();

            builder.Services.AddSerilog();

            builder.Configuration.AddJsonFile("rabbitMqSettings.json", optional: false, reloadOnChange: true);
            builder.Configuration.AddJsonFile("pmiReportDistributionSettings.json", optional: false, reloadOnChange: true);

            builder.Services.Configure<PmiReportDistributionSettings>(builder.Configuration.GetSection("PmiReportDistributionSettings"));

            builder.Services.AddSingleton<IConnectionFactory>(_ =>
            {
                string hostName = builder.Configuration["RabbitMQSettings:HostName"]!;
                int port = int.Parse(builder.Configuration["RabbitMQSettings:Port"]!);
                string virtualHost = builder.Configuration["RabbitMQSettings:VirtualHost"]!;
                string userName = builder.Configuration["RabbitMQSettings:UserName"]!;
                string password = builder.Configuration["RabbitMQSettings:Password"]!;

                ConnectionFactory factory = new()
                {
                    HostName = hostName,
                    Port = port,
                    VirtualHost = virtualHost,
                    UserName = userName,
                    Password = password,
                    Ssl =
                    {
                        Enabled = true,
                        ServerName = hostName,
                        Version = SslProtocols.Tls12
                    },
                };

                return factory;
            });

            builder.Services.AddHostedService<PmiReportEmailDistributionMessageConsumer>();

            builder.Services.AddScoped<IMessageDispatcher, NuclearEvaluationMessageDispatcher>();


            WebApplication app = builder.Build();

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