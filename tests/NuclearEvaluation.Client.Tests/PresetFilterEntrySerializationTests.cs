using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using Shouldly;

namespace NuclearEvaluation.Client.Tests;

public class PresetFilterEntrySerializationTests
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    [Fact]
    public void Serialize_ShouldNotExposePresetFilterNavigation()
    {
        PresetFilter filter = new()
        {
            Id = 12,
            Name = "saved filter",
        };
        PresetFilterEntry entry = new()
        {
            Id = 34,
            PresetFilterEntryType = PresetFilterEntryType.Sample,
            PresetFilterId = filter.Id,
            PresetFilter = filter,
            SerializedDescriptors = "[]",
        };
        filter.Entries.Add(entry);

        string json = JsonSerializer.Serialize(filter, JsonOptions);

        json.ShouldNotContain("\"presetFilter\":");
        json.ShouldContain("presetFilterId");
    }

    [Fact]
    public void Deserialize_WithLegacyNullPresetFilter_ShouldKeepEntryPayload()
    {
        const string json = """
            {
              "id": 0,
              "name": "sobaker",
              "entries": [
                {
                  "id": 0,
                  "presetFilterEntryType": 2,
                  "logicalFilterOperator": 0,
                  "isEnabled": true,
                  "presetFilterId": 0,
                  "presetFilter": null,
                  "serializedDescriptors": "[{\"Property\":\"Sample.Sequence\",\"Type\":\"System.String\",\"FilterValue\":\"b\",\"FilterOperator\":6,\"LogicalFilterOperator\":0,\"Filters\":null}]"
                }
              ]
            }
            """;

        PresetFilter? filter = JsonSerializer.Deserialize<PresetFilter>(json, JsonOptions);

        filter.ShouldNotBeNull();
        filter.Name.ShouldBe("sobaker");
        PresetFilterEntry entry = filter.Entries.Single();
        entry.PresetFilter.ShouldBeNull();
        entry.PresetFilterEntryType.ShouldBe(PresetFilterEntryType.Sample);
        entry.SerializedDescriptors.ShouldContain("Sample.Sequence");
        entry.IsCorrupted.ShouldBeFalse();
    }

    [Fact]
    public void PresetFilterNavigation_ShouldRemainNullableForApiModelValidation()
    {
        PropertyInfo property = typeof(PresetFilterEntry).GetProperty(nameof(PresetFilterEntry.PresetFilter))!;

        NullabilityInfo nullability = new NullabilityInfoContext().Create(property);

        nullability.WriteState.ShouldBe(NullabilityState.Nullable);
    }
}
