using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;

namespace NuclearEvaluation.Client.Services;

public interface IPresetFilterComponent
{
    PresetFilterEntry PresetFilterEntry { get; set; }
    void Reset();
    string? FilterString { get; }
    PresetFilterEntryType EntryType { get; }
}