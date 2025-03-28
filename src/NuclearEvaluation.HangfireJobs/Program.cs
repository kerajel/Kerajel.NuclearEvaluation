using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Jobs;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.HangfireJobs;

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
                .UseSqlServerStorage(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"),
                    new SqlServerStorageOptions
                    {
                        SchemaName = builder.Configuration["HangfireSettings:DbSchemaName"],
                    }
                );
        });

        builder.Services.AddTransient<IEnqueueStemReportForPublishingJob, EnqueueStemReportForPublishingJob>();

        builder.Services.AddHangfireServer();

        builder.Services.AddDbContext<NuclearEvaluationServerDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("NuclearEvaluationServerDbConnection"));
        }, ServiceLifetime.Scoped);

        WebApplication app = builder.Build();

        app.MapHangfireDashboard();

        JobScheduler.RegisterJobs();

        app.Run();
    }
}