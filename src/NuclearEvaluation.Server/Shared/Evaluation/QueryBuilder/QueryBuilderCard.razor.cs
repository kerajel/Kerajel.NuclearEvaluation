using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Server.Shared.Grids;
using Radzen;

namespace NuclearEvaluation.Server.Shared.Evaluation.QueryBuilder;

public partial class QueryBuilderCard : ComponentBase
{

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = null!;

    SeriesQueryBuilderFilter _seriesFilter = null!;
    SampleQueryBuilderFilter _sampleFilter = null!;
    SubSampleQueryBuilderFilter _subSampleFilter = null!;
    ParticleQueryBuilderFilter _particleFilter = null!;
    ApmQueryBuilderFilter _apmFilter = null!;

    IDataGrid _activeGrid = null!;

    SeriesGrid _seriesGrid = null!;
    SampleGrid _sampleGrid = null!;
    SubSampleGrid _subSampleGrid = null!;
    ApmGrid _apmGrid = null!;
    ParticleGrid _particleGrid = null!;

    PresetFilterBox _presetFilterBox = new();
    PresetFilter _activeFilter = new();

    IDataGrid[] _dataGrids =>
    [
        _seriesGrid,
        _sampleGrid,
        _subSampleGrid,
        _apmGrid,
        _particleGrid,
    ];

    IPresetFilterComponent[]? _presetFilterComponents;
    IPresetFilterComponent[] PresetFilterComponents
    {
        get
        {
            _presetFilterComponents ??=
                [
                    _seriesFilter,
                    _sampleFilter,
                    _subSampleFilter,
                    _apmFilter,
                    _particleFilter,
                ];
            return _presetFilterComponents.Where(x => x != null).ToArray();
        }
    }

    string HiddenStyle { get; } = "color: grey; font-size: 85%;";
    string CheckBoxWithHeadingStyle { get; } = "display: flex; align-items: center;";
    string CheckBoxStyle { get; } = "margin-right: 10px;";
    string HeadingSize { get; } = "H4";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _activeGrid = _seriesGrid;
            await _activeGrid.Reset();
            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    PresetFilter GetActivePresetFilter()
    {
        _activeFilter.Entries = PresetFilterComponents
            .Select(x => x.PresetFilterEntry)
            .Where(x => x.Descriptors.Any())
            .ToList();

        return _activeFilter;
    }

    public PresetFilterBox GetPresetFilterBox()
    {
        PresetFilterBox presetFilterBox = new PresetFilterBox();
        foreach (IPresetFilterComponent filterComponent in PresetFilterComponents)
        {
            PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(filterComponent.EntryType);
            if (filterEntry.IsEnabled)
            {
                presetFilterBox.Set(filterComponent.EntryType, filterComponent.FilterString);
            }
        }
        return presetFilterBox;
    }


    void OnPresetFilterCheckBoxChanged(bool value, PresetFilterEntryType entryType)
    {
        PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(entryType);
        filterEntry.IsEnabled = value;

        StateHasChanged();
    }

    bool GetFilterCheckBoxVisibility(PresetFilterEntryType entryType)
    {
        PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(entryType);
        return filterEntry.IsEnabled;
    }

    bool GetDataGridVisibility(IDataGrid? dataGrid)
    {
        if (dataGrid == null)
        {
            return false;
        }
        return _activeGrid == dataGrid;
    }

    void OnPresetFilterSelected(PresetFilter presetFilter)
    {
        _activeFilter = presetFilter;
        ReRenderPresetFilters(_activeFilter);
    }

    void ReRenderPresetFilters(PresetFilter presetFilter)
    {
        Dictionary<PresetFilterEntryType, PresetFilterEntry> entryDict = presetFilter.Entries
            .ToDictionary(x => x.PresetFilterEntryType);

        foreach (IPresetFilterComponent filterComponent in PresetFilterComponents)
        {
            if (entryDict.TryGetValue(filterComponent.EntryType, out PresetFilterEntry? entry))
            {
                filterComponent.PresetFilterEntry = entry;
            }
            else
            {
                filterComponent.Reset();
            }
        }

        StateHasChanged();
    }

    async Task ApplyPresetFilter()
    {
        await JSRuntime.InvokeVoidAsync("forceUpdateNumericInputs");
        await _activeGrid.Reset(false, false);
    }

    async Task OnDataGridSelected(IDataGrid dataGrid)
    {
        await dataGrid.Reset();
        _activeGrid = dataGrid;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }
}