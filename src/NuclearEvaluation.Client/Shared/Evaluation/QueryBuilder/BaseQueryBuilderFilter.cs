using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Extensions;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Client.Services;

namespace NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;

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
            presetFilterEntry.Descriptors = GetCurrentFilters();
            presetFilterEntry.LogicalFilterOperator = logicalFilterOperator;
            return presetFilterEntry;
        }
        set
        {
            presetFilterEntry = value;
            currentFilters = value.Descriptors?.ToArray() ?? [];
            logicalFilterOperator = value.LogicalFilterOperator;
            RefreshFilter();
            StateHasChanged();
        }
    }

    public abstract PresetFilterEntryType EntryType { get; }

    public string? FilterString
    {
        get
        {
            IEnumerable<CompositeFilterDescriptor> filters = GetCurrentFilters();
            return filters.IsNullOrEmpty()
                ? null
                : filters.ToFilterString<TItem>(
                    logicalFilterOperator,
                    filter?.FilterCaseSensitivity ?? FilterCaseSensitivity.Default);
        }
    }

    protected RadzenDataFilter<TItem> filter = null!;
    protected PresetFilterEntry? presetFilterEntry;
    protected LogicalFilterOperator logicalFilterOperator = LogicalFilterOperator.And;
    IEnumerable<CompositeFilterDescriptor> currentFilters = [];

    public void Reset()
    {
        presetFilterEntry = PresetFilterEntry.Create(EntryType, [], true);
        logicalFilterOperator = LogicalFilterOperator.And;
        currentFilters = [];
        RefreshFilter();
    }

    IEnumerable<CompositeFilterDescriptor> GetCurrentFilters()
    {
        if (filter is not null)
        {
            currentFilters = filter.Filters?.ToArray() ?? [];
            logicalFilterOperator = filter.LogicalFilterOperator;
        }

        return currentFilters;
    }

    void RefreshFilter()
    {
        if (filter is null)
        {
            return;
        }

        filter.Filters = currentFilters;
        filter.Filter();
    }
}
