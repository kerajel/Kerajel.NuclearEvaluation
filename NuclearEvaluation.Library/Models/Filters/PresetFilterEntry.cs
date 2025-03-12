using NuclearEvaluation.Kernel.Converters;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.Filters;

[Index(nameof(PresetFilterId))]
public class PresetFilterEntry
{
    static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new CompositeFilterDescriptorConverter(),
        }
    };

    string _serializedDescriptors = string.Empty;
    IEnumerable<CompositeFilterDescriptor> _descriptors = [];

    public static PresetFilterEntry Create(
        PresetFilterEntryType presetFilterEntryType,
        IEnumerable<CompositeFilterDescriptor> descriptors,
        bool isEnabled = true)
    {
        PresetFilterEntry result = new()
        {
            PresetFilterEntryType = presetFilterEntryType,
            Descriptors = descriptors?.ToArray() ?? [],
            IsEnabled = isEnabled,
        };
        return result;
    }

    [Key]
    public int Id { get; set; }

    [Required]
    public PresetFilterEntryType PresetFilterEntryType { get; set; }

    [Required]
    public LogicalFilterOperator LogicalFilterOperator { get; set; } = LogicalFilterOperator.And;

    [Required]
    public bool IsEnabled { get; set; } = false;

    [Required]
    [ForeignKey(nameof(PresetFilter))]
    public int PresetFilterId { get; set; }

    public PresetFilter PresetFilter { get; set; } = null!;

    [NotMapped]
    [JsonIgnore]
    public bool IsCorrupted { get; set; }

    [NotMapped]
    [JsonIgnore]
    public IEnumerable<CompositeFilterDescriptor> Descriptors
    {
        get
        {
            return _descriptors;
        }
        set
        {
            _serializedDescriptors = JsonSerializer.Serialize(value, _serializerOptions);
            _descriptors = value;
        }
    }

    public string SerializedDescriptors
    {
        get
        {
            return _serializedDescriptors;
        }
        set
        {
            bool deserialized = JsonExtensions.TryDeserialize(value, out ICollection<CompositeFilterDescriptor>? descriptors, _serializerOptions);
            if (deserialized)
            {
                _descriptors = descriptors!;
                IsCorrupted = false;
            }
            else
            {
                IsCorrupted = true;
            }
        }
    }
}