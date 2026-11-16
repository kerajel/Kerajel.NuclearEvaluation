using System.Net.Http.Headers;
using System.Text.Json;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class OpenAiAssistantClient(HttpClient httpClient, IConfiguration configuration)
{
    static readonly Uri ResponsesUrl = new("https://api.openai.com/v1/responses");

    string? ApiKey =>
        configuration["OpenAI:ApiKey"]?.Trim() is { Length: > 0 } configuredKey
            ? configuredKey
            : configuration["OPENAI_API_KEY"]?.Trim();

    public bool IsConfigured => ApiKey is { Length: > 0 };

    public async Task<JsonElement[]> RequestAsync(
        string instructions,
        string input,
        IReadOnlyList<object> tools,
        CancellationToken ct
    )
    {
        if (ApiKey is not { Length: > 0 } apiKey)
            throw new InvalidOperationException("The OpenAI API key is not configured.");

        string model = configuration["OpenAI:Model"]?.Trim() is { Length: > 0 } configuredModel
            ? configuredModel
            : "gpt-5-mini";
        using HttpRequestMessage request = new(HttpMethod.Post, ResponsesUrl)
        {
            Content = JsonContent.Create(
                new
                {
                    model,
                    instructions,
                    input,
                    tools,
                    tool_choice = "auto",
                    parallel_tool_calls = false,
                    max_output_tokens = 2_048,
                    store = false,
                }
            ),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        ResponsePayload? payload = await response.Content.ReadFromJsonAsync<ResponsePayload>(
            cancellationToken: ct
        );
        return payload?.Output
            ?? throw new InvalidDataException("The assistant response is missing output.");
    }

    public static OpenAiToolCall? ReadToolCall(JsonElement[] output)
    {
        JsonElement[] calls = output
            .Where(item => ReadString(item, "type") == "function_call")
            .ToArray();
        return
            calls.Length == 1
            && ReadString(calls[0], "name") is { Length: > 0 } name
            && ReadString(calls[0], "arguments") is { Length: > 0 and <= 12_288 } arguments
            ? new OpenAiToolCall(name, arguments)
            : null;
    }

    static string? ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    sealed record ResponsePayload(JsonElement[] Output);
}

internal sealed record OpenAiToolCall(string Name, string Arguments);
