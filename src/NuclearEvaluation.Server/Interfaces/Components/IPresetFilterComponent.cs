﻿using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Models.Filters;

namespace NuclearEvaluation.Server.Interfaces.Components;

public interface IPresetFilterComponent
{
    PresetFilterEntry PresetFilterEntry { get; set; }
    void Reset();
    string? FilterString { get; }
    PresetFilterEntryType EntryType { get; }
}