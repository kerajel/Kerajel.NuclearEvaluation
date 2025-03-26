using Hangfire;
using Hangfire.SqlServer;

namespace NuclearEvaluation.HangfireJobs;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);

        webApplicationBuilder.Services.AddHangfire(configuration =>
        {
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    "Server=localhost;Connection Timeout=30;Command Timeout=30;Persist Security Info=False;TrustServerCertificate=True;Integrated Security=True;Initial Catalog=NuclearEvaluationServer;MultipleActiveResultSets=True;",
                    new SqlServerStorageOptions
                    {
                        SchemaName = "HANGFIRE",

                    }
                );
        });

        webApplicationBuilder.Services.AddHangfireServer();

        WebApplication app = webApplicationBuilder.Build();

        app.MapHangfireDashboard();

        JobScheduler.RegisterJobs();

        app.Run();
    }
}

public class DemoJob
{
    public void RunDemoTask(string message)
    {
        Console.WriteLine($"Running DemoJob with message: {message}");
        Thread.Sleep(1000);
        Console.WriteLine("DemoJob done.");
    }
}
