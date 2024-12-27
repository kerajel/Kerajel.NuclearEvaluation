using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Models.Filters;

namespace NuclearEvaluation.Library.Interfaces;

public interface IPresetFilterComponent
{
    PresetFilterEntry PresetFilterEntry { get; set; }
    void Reset();
    string? FilterString { get; }
    PresetFilterEntryType EntryType { get; }
}