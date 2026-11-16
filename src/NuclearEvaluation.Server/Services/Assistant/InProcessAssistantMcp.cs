using System.IO.Pipelines;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class InProcessAssistantMcp(
    IOptions<McpServerOptions> serverOptions,
    IServiceProvider services
)
{
    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct
    )
    {
        Pipe requests = new();
        Pipe responses = new();
        await using Stream clientInput = requests.Writer.AsStream();
        await using Stream clientOutput = responses.Reader.AsStream();
        await using StreamServerTransport serverTransport = new(
            requests.Reader.AsStream(),
            responses.Writer.AsStream()
        );
        await using McpServer server = McpServer.Create(
            serverTransport,
            serverOptions.Value,
            serviceProvider: services
        );
        using CancellationTokenSource shutdown = CancellationTokenSource.CreateLinkedTokenSource(
            ct
        );
        Task serving = server.RunAsync(shutdown.Token);
        try
        {
            await using McpClient client = await McpClient.CreateAsync(
                new StreamClientTransport(clientInput, clientOutput),
                cancellationToken: ct
            );
            return await client.CallToolAsync(toolName, arguments, cancellationToken: ct);
        }
        finally
        {
            await shutdown.CancelAsync();
            try
            {
                await serving;
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                // Stopping the private MCP session after the call cancels the server loop.
            }
        }
    }
}
