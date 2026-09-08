using System.ComponentModel.DataAnnotations;
using NuclearEvaluation.Shared.Enums;

namespace NuclearEvaluation.Shared.Models.Filters;

public class PresetFilter : IValidatableObject
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public List<PresetFilterEntry> Entries { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length is < 5 or > 25)
            yield return new("Preset name must be between 5 and 25 characters.", [nameof(Name)]);
        if (
            Entries is not null
            && (
                Entries.Any(entry =>
                    entry is null
                    || entry.IsCorrupted
                    || entry.SerializedDescriptors.Length > 16_384
                )
                || Entries.Select(entry => entry.PresetFilterEntryType).Distinct().Count()
                    != Entries.Count
            )
        )
            yield return new(
                "Preset entries must be valid and have distinct filter types.",
                [nameof(Entries)]
            );
    }

    public PresetFilterEntry EnsurePresetFilterEntry(PresetFilterEntryType type)
    {
        PresetFilterEntry? sampleEntry = Entries.FirstOrDefault(x =>
            x.PresetFilterEntryType == type
        );

        if (sampleEntry == null)
        {
            sampleEntry = new() { PresetFilterEntryType = type };
            Entries.Add(sampleEntry);
        }

        return sampleEntry;
    }
}
