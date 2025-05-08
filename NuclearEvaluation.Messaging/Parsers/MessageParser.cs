using Kerajel.Primitives.Models;
using System.Text;
using System.Text.Json;

namespace NuclearEvaluation.Messaging.Parsers;

public static class MessageParser
{
    public static OperationResult<T> TryParseMessage<T>(ReadOnlyMemory<byte> body)
    {
        try
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
            T message = JsonSerializer.Deserialize<T>(body.Span, options)!;
            return OperationResult<T>.Succeeded(message);
        }
        catch
        {
            return OperationResult<T>.Faulted();
        }
    }
}
