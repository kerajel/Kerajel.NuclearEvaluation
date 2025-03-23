using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Server.Shared.Evaluation.QueryBuilder;

public abstract class BaseQueryBuilderFilter<TItem> : ComponentBase, IPresetFilterComponent where TItem : class, new()
{
    [Parameter]
    public bool Visible { get; set; }

    protected virtual string DecimalFormat => "{0:0.##}";
    protected virtual string DateOnlyFormat => "{0:yyyy-MM-dd}";

    public PresetFilterEntry PresetFilterEntry
    {
        get
        {
            presetFilterEntry ??= PresetFilterEntry.Create(EntryType, [], true);
            presetFilterEntry.Descriptors = filter.Filters;
            presetFilterEntry.LogicalFilterOperator = filter.LogicalFilterOperator;
            return presetFilterEntry;
        }
        set
        {
            presetFilterEntry = value;
            filter.Filters = value.Descriptors;
            logicalFilterOperator = value.LogicalFilterOperator;
            StateHasChanged();
        }
    }

    public abstract PresetFilterEntryType EntryType { get; }

    public string? FilterString
    {
        get
        {
            return filter.Filters.IsNullOrEmpty() ? null : filter.ToFilterString();
        }
    }

    protected RadzenDataFilter<TItem> filter = null!;
    protected PresetFilterEntry? presetFilterEntry;
    protected LogicalFilterOperator logicalFilterOperator = LogicalFilterOperator.And;

    public void Reset()
    {
        presetFilterEntry = PresetFilterEntry.Create(EntryType, [], true);
        logicalFilterOperator = LogicalFilterOperator.And;
        filter.Filters = [];
    }
}