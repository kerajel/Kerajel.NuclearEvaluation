using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public class InProcessAssistantMcpTests
{
    [Fact]
    public async Task ProductionUsesPrivateMcpForItsCachedCatalogAndActionValidation()
    {
        ServiceCollection services = new();
        services.AddMcpServer().WithTools<LocalSeriesTools>().WithTools<LocalSiteAssistantTools>();
        services.AddSingleton<InProcessAssistantMcp>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using ConfigurationManager configuration = new();
        configuration["Assistant:McpUrl"] = "invalid loopback URL";
        using HttpClient httpClient = new();
        using AssistantCatalogCache<SeriesSchemaDescription> seriesCache = new();
        using AssistantCatalogCache<SiteAssistantCatalog> siteCache = new();
        LocalSiteAssistantClient tools = new(
            new LocalAssistantToolsClient(
                httpClient,
                configuration,
                environment,
                provider.GetRequiredService<InProcessAssistantMcp>(),
                seriesCache
            ),
            siteCache
        );
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        SiteAssistantCatalog catalog = await tools.DescribeAsync(timeout.Token);
        Assert.NotEmpty(catalog.Grids);
        Assert.Same(catalog, await tools.DescribeAsync(timeout.Token));

        SiteAssistantContext calendar = new(
            AssistantPage.FleetGeneration,
            AssistantSurface.GenerationCalendar
        );
        AssistantAction action = new(AssistantActionKind.SelectGenerationView)
        {
            GenerationSelection = new("IL", 2, true),
        };
        SiteAssistantReply? validated = await tools.ValidateAsync(calendar, action, timeout.Token);
        Assert.Equal(action, validated?.Action);
        Assert.Null(
            await tools.ValidateAsync(
                new(AssistantPage.Fleet, AssistantSurface.None),
                action,
                timeout.Token
            )
        );
    }
}
