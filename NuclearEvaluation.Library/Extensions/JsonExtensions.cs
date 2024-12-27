using System.Text.Json;

namespace NuclearEvaluation.Library.Extensions;

public static class JsonExtensions
{
    public static bool TryDeserialize<T>(string json, out T? result, JsonSerializerOptions? options = null)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(json)) 
            return false;
        
        try
        {
            result = JsonSerializer.Deserialize<T>(json, options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}