using NuclearEvaluation.Kernel.Enums;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Filters;

public class PresetFilter
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public List<PresetFilterEntry> Entries { get; set; } = [];

    public PresetFilterEntry EnsurePresetFilterEntry(PresetFilterEntryType type)
    {
        PresetFilterEntry? sampleEntry = Entries
            .FirstOrDefault(x => x.PresetFilterEntryType == type);

        if (sampleEntry == null)
        {
            sampleEntry = new()
            {
                PresetFilterEntryType = type,
            };
            Entries.Add(sampleEntry);
        }

        return sampleEntry;
    }
}