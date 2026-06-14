using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using Radzen;

namespace NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;

public partial class QueryBuilderCard : ComponentBase
{

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = null!;

    SeriesQueryBuilderFilter _seriesFilter = null!;
    SampleQueryBuilderFilter _sampleFilter = null!;
    SubSampleQueryBuilderFilter _subSampleFilter = null!;
    ParticleQueryBuilderFilter _particleFilter = null!;
    ApmQueryBuilderFilter _apmFilter = null!;

    IDataGridComponent _activeGrid = null!;

    SeriesGrid _seriesGrid = null!;
    SampleGrid _sampleGrid = null!;
    SubSampleGrid _subSampleGrid = null!;
    ApmGrid _apmGrid = null!;
    ParticleGrid _particleGrid = null!;

    PresetFilterBox _presetFilterBox = new();
    PresetFilter _activeFilter = new();

    IDataGridComponent[] _dataGrids =>
    [
        _seriesGrid,
        _sampleGrid,
        _subSampleGrid,
        _apmGrid,
        _particleGrid,
    ];

    IPresetFilterComponent[] PresetFilterComponents
    {
        get
        {
            IPresetFilterComponent?[] components =
            [
                _seriesFilter,
                _sampleFilter,
                _subSampleFilter,
                _apmFilter,
                _particleFilter,
            ];
            return components.Where(x => x != null).ToArray()!;
        }
    }

    string HiddenStyle { get; } = "color: grey; font-size: 85%;";
    string CheckBoxWithHeadingStyle { get; } = "display: flex; align-items: center;";
    string CheckBoxStyle { get; } = "margin-right: 10px;";
    string HeadingSize { get; } = "H4";
    string ToolbarStyle { get; } = "display: flex; align-items: end; justify-content: space-between; gap: 12px; flex-wrap: wrap;";
    string ToolbarGroupStyle { get; } = "display: flex; flex-direction: column; gap: 4px; min-width: 220px;";
    string PresetToolbarGroupStyle { get; } = "display: flex; flex-direction: column; gap: 4px; min-width: 420px; flex: 1 1 420px;";
    string ToolbarLabelStyle { get; } = "font-size: 0.875rem; margin-bottom: 0;";

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
            string? filterString = filterComponent.FilterString;
            if (filterEntry.IsEnabled && !string.IsNullOrWhiteSpace(filterString))
            {
                presetFilterBox.Set(filterComponent.EntryType, filterString);
            }
        }
        return presetFilterBox;
    }


    async Task OnPresetFilterCheckBoxChanged(bool value, PresetFilterEntryType entryType)
    {
        PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(entryType);
        filterEntry.IsEnabled = value;

        StateHasChanged();
        await ReloadActiveGrid();
    }

    bool GetFilterCheckBoxVisibility(PresetFilterEntryType entryType)
    {
        PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(entryType);
        return filterEntry.IsEnabled;
    }

    bool GetDataGridVisibility(IDataGridComponent? dataGrid)
    {
        if (dataGrid == null)
        {
            return false;
        }
        return _activeGrid == dataGrid;
    }

    async Task OnPresetFilterSelected(PresetFilter presetFilter)
    {
        _activeFilter = presetFilter;
        ReRenderPresetFilters(_activeFilter);
        await ReloadActiveGrid();
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
        await ReloadActiveGrid();
    }

    async Task ReloadActiveGrid()
    {
        if (_activeGrid is null)
        {
            return;
        }

        await JSRuntime.InvokeVoidAsync("forceUpdateNumericInputs");
        await _activeGrid.Reset(false, false);
    }

    async Task OnDataGridSelected(IDataGridComponent dataGrid)
    {
        _activeGrid = dataGrid;
        await InvokeAsync(StateHasChanged);
        await dataGrid.Reset();
        await Task.Yield();
    }
}
