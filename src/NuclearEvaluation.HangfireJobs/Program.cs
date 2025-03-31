using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using NuclearEvaluation.HangfireJobs.Jobs;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Models.Settings;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.HangfireJobs
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
                            SchemaName = builder.Configuration["HangfireSettings:DbSchemaName"],
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
                    rabbitMqConfigurator.Host(
                        new Uri(builder.Configuration["RabbitMQSettings:HostUri"]!),
                        hostConfigurator =>
                        {
                            hostConfigurator.Username(builder.Configuration["RabbitMQSettings:UserName"]!);
                            hostConfigurator.Password(builder.Configuration["RabbitMQSettings:Password"]!);
                        }
                    );
                });
            });

            builder.Services.Configure<PmiReportDistributionSettings>(
                builder.Configuration.GetSection("PmiReportDistributionSettings"));

            builder.Services.AddTransient<IEnqueueStemReportForPublishingJob, EnqueueStemReportForPublishingJob>();
            builder.Services.AddTransient<NuclearEvaluation.HangfireJobs.Services.PmiReportDistributionMessageDispatcher>();

            WebApplication app = builder.Build();

            app.MapHangfireDashboard();

            JobScheduler.RegisterJobs();

            app.Run();
        }
    }
}