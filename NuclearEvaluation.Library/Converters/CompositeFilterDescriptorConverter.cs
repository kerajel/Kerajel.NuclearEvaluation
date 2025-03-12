using Radzen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuclearEvaluation.Kernel.Converters;

public class CompositeFilterDescriptorConverter : JsonConverter<CompositeFilterDescriptor>
{
    const string _typePropertyName = "Type";

    public override CompositeFilterDescriptor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        CompositeFilterDescriptor descriptor = new()
        {
            Property = root.GetProperty(nameof(CompositeFilterDescriptor.Property)).GetString(),
            FilterOperator = root.TryGetProperty(nameof(CompositeFilterDescriptor.FilterOperator), out JsonElement fo)
                ? fo.Deserialize<FilterOperator?>(options)
                : null,
            LogicalFilterOperator = root.TryGetProperty(nameof(CompositeFilterDescriptor.LogicalFilterOperator), out JsonElement lfo)
                ? lfo.Deserialize<LogicalFilterOperator>(options)
                : default,
            Filters = root.TryGetProperty(nameof(CompositeFilterDescriptor.Filters), out JsonElement filters) && filters.ValueKind == JsonValueKind.Array
                    ? filters.EnumerateArray().Select(f => JsonSerializer.Deserialize<CompositeFilterDescriptor>(f.GetRawText(), options)).ToArray()
                    : null
        };

        if (root.TryGetProperty(_typePropertyName, out JsonElement typeProp) &&
            root.TryGetProperty(nameof(CompositeFilterDescriptor.FilterValue), out JsonElement valueProp))
        {
            Type type = Type.GetType(typeProp.GetString()) ?? typeof(object);
            descriptor.FilterValue = JsonSerializer.Deserialize(valueProp.GetRawText(), type, options);
        }

        return descriptor;
    }

    public override void Write(Utf8JsonWriter writer, CompositeFilterDescriptor value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(CompositeFilterDescriptor.Property), value.Property);
        if (value.FilterValue != null)
        {
            writer.WriteString(_typePropertyName, value.FilterValue.GetType().FullName);
            writer.WritePropertyName(nameof(CompositeFilterDescriptor.FilterValue));
            JsonSerializer.Serialize(writer, value.FilterValue, value.FilterValue.GetType(), options);
        }
        else
        {
            writer.WriteNull(nameof(CompositeFilterDescriptor.FilterValue));
        }
        if (value.FilterOperator.HasValue)
        {
            writer.WriteNumber(nameof(CompositeFilterDescriptor.FilterOperator), (int)value.FilterOperator.Value);
        }
        writer.WriteNumber(nameof(CompositeFilterDescriptor.LogicalFilterOperator), (int)value.LogicalFilterOperator);
        writer.WritePropertyName(nameof(CompositeFilterDescriptor.Filters));
        JsonSerializer.Serialize(writer, value.Filters, options);
        writer.WriteEndObject();
    }
}