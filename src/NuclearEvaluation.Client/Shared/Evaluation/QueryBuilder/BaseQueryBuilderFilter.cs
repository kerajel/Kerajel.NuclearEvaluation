using System.Globalization;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Extensions;
using NuclearEvaluation.Shared.Models.Filters;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;

public abstract class BaseQueryBuilderFilter<TItem> : ComponentBase, IPresetFilterComponent
    where TItem : class, new()
{
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public EventCallback OnDraftEdited { get; set; }

    protected Task NotifyDraftEdited()
    {
        if (filter is not null)
            logicalFilterOperator = filter.LogicalFilterOperator;

        return OnDraftEdited.InvokeAsync();
    }

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
            NormalizeEnumValues(currentFilters);
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
                    filter?.FilterCaseSensitivity ?? FilterCaseSensitivity.Default
                );
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

    static void NormalizeEnumValues(IEnumerable<CompositeFilterDescriptor> descriptors)
    {
        foreach (CompositeFilterDescriptor descriptor in descriptors)
        {
            // Radzen matches selections to boxed integer keys, including for enum properties.
            if (descriptor.FilterValue is Enum enumValue)
                descriptor.FilterValue = Convert.ToInt32(enumValue, CultureInfo.InvariantCulture);
            if (descriptor.Filters is { } children)
                NormalizeEnumValues(children);
        }
    }

    void RefreshFilter()
    {
        if (filter is null)
        {
            return;
        }

        filter.Filters = currentFilters;
#pragma warning disable BL0005 // A restored filter must use its logical operator before the next render.
        filter.LogicalFilterOperator = logicalFilterOperator;
#pragma warning restore BL0005
    }
}
