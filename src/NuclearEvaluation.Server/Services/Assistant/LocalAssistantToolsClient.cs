using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class LocalAssistantToolsClient(
    HttpClient httpClient,
    IConfiguration configuration,
    AssistantCatalogCache<SeriesSchemaDescription> schemaCache
)
{
    const string DefaultEndpoint = "http://127.0.0.1:8081/mcp";
    static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(10);
    internal static readonly JsonSerializerOptions McpJson = new(JsonSerializerOptions.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter<SeriesType>(),
            new JsonStringEnumConverter<SeriesQueryEntity>(),
            new JsonStringEnumConverter<SeriesComparison>(),
            new JsonStringEnumConverter<StemQualityCheck>(),
        },
    };

    public Task<SeriesSchemaDescription> GetSeriesSchemaAsync(CancellationToken ct) =>
        schemaCache.GetAsync(DescribeSeriesSchemaAsync, ct);

    async Task<SeriesSchemaDescription> DescribeSeriesSchemaAsync(CancellationToken ct)
    {
        CallToolResult response = await CallToolAsync(
            SeriesMcpContract.DescribeSchemaToolName,
            arguments: null,
            ct
        );
        if (response.IsError is true)
            throw new InvalidOperationException(
                "The local MCP schema tool could not describe the Series grid."
            );

        SeriesSchemaDescription? schema =
            response.StructuredContent?.Deserialize<SeriesSchemaDescription>(McpJson);
        if (
            schema?.Entities is not { Count: > 0 } entities
            || schema.CountComparisons is not { Count: > 0 }
            || entities.Any(entity =>
                entity is null
                || string.IsNullOrWhiteSpace(entity.Entity)
                || string.IsNullOrWhiteSpace(entity.RelationshipPath)
                || entity.Fields is not { Count: > 0 } fields
                || fields.Any(field =>
                    field is null
                    || string.IsNullOrWhiteSpace(field.Name)
                    || string.IsNullOrWhiteSpace(field.ValueType)
                    || field.Comparisons is not { Count: > 0 }
                    || field.EnumValues is null
                )
            )
        )
        {
            throw new InvalidOperationException(
                "The local MCP tool returned an invalid Series grid schema."
            );
        }

        return schema;
    }

    public async Task<SeriesGridQuery?> BuildSeriesQueryAsync(
        SeriesGridQuery query,
        CancellationToken ct
    )
    {
        CallToolResult response = await CallToolAsync(
            SeriesMcpContract.BuildQueryToolName,
            new Dictionary<string, object?> { ["query"] = query },
            ct
        );
        if (response.IsError is true)
            return null;

        return response.StructuredContent?.Deserialize<LocalSeriesToolResult>(McpJson)?.Query
            ?? throw new InvalidOperationException(
                "The local MCP tool did not return a Series grid query."
            );
    }

    internal async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct
    )
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ToolTimeout);
        await using HttpClientTransport transport = new(
            new HttpClientTransportOptions
            {
                Endpoint = GetEndpoint(),
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
            },
            httpClient
        );

        try
        {
            await using McpClient client = await McpClient.CreateAsync(
                transport,
                cancellationToken: timeout.Token
            );
            return await client.CallToolAsync(
                toolName,
                arguments,
                cancellationToken: timeout.Token
            );
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The local MCP request timed out.", ex);
        }
    }

    Uri GetEndpoint()
    {
        string endpoint = configuration["Assistant:McpUrl"] ?? DefaultEndpoint;
        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https")
            || !uri.IsLoopback
            || uri.AbsolutePath != "/mcp"
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
        )
        {
            throw new InvalidOperationException(
                "Assistant:McpUrl must use HTTP(S) on loopback at /mcp."
            );
        }
        return uri;
    }
}
