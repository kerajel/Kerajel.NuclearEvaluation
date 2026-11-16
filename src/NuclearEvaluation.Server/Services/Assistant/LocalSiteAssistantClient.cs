using System.Text.Json;
using ModelContextProtocol.Protocol;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class LocalSiteAssistantClient(
    LocalAssistantToolsClient transport,
    AssistantCatalogCache<SiteAssistantCatalog> cache
)
{
    public Task<SiteAssistantCatalog> DescribeAsync(CancellationToken ct) =>
        cache.GetAsync(FetchCatalogAsync, ct);

    async Task<SiteAssistantCatalog> FetchCatalogAsync(CancellationToken ct) =>
        await InvokeAsync<SiteAssistantCatalog>("describe_site_assistant", null, ct)
        ?? throw new InvalidOperationException("The local MCP catalog could not be loaded.");

    public Task<SiteAssistantReply?> ValidateAsync(
        SiteAssistantContext context,
        AssistantAction action,
        CancellationToken ct
    ) =>
        InvokeAsync<SiteAssistantReply>(
            "validate_site_assistant_action",
            new Dictionary<string, object?> { ["context"] = context, ["action"] = action },
            ct
        );

    public Task<SiteAssistantReply?> HelpAsync(
        SiteAssistantContext context,
        string topic,
        CancellationToken ct
    ) =>
        InvokeAsync<SiteAssistantReply>(
            "get_site_help",
            new Dictionary<string, object?> { ["context"] = context, ["topic"] = topic },
            ct
        );

    async Task<T?> InvokeAsync<T>(
        string name,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct
    )
        where T : class
    {
        CallToolResult result = await transport.CallToolAsync(name, arguments, ct);
        return result.IsError is true
            ? null
            : result.StructuredContent?.Deserialize<T>(LocalAssistantToolsClient.McpJson)
                ?? throw new InvalidOperationException("The local MCP tool returned no result.");
    }
}
