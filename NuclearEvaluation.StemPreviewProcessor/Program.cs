using NuclearEvaluation.Kernel.Models.Messaging;

namespace StemPreviewProcessor;

public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();

        builder.Configuration.AddJsonFile("rabbitMqSettings.json", optional: false, reloadOnChange: true);
        builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection(nameof(RabbitMQSettings)));

        IHost host = builder.Build();
        host.Run();
    }
}