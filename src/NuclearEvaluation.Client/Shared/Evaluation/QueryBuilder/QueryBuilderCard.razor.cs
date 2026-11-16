using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;

namespace NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;

public partial class QueryBuilderCard : ComponentBase
{
    static readonly QueryBuilderExample[] Examples =
    [
        new(
            "Regular series with QC samples",
            SeriesQueryEntity.Series,
            [
                new(
                    SeriesQueryEntity.Series,
                    new(
                        nameof(SeriesView.SeriesType),
                        SeriesComparison.Equal,
                        nameof(SeriesType.Regular)
                    )
                ),
                new(
                    SeriesQueryEntity.Samples,
                    new(
                        nameof(SampleView.SampleType),
                        SeriesComparison.Equal,
                        nameof(SampleType.Qc)
                    )
                ),
            ]
        ),
        new(
            "Samples in NU series",
            SeriesQueryEntity.Samples,
            [
                new(
                    SeriesQueryEntity.Series,
                    new(nameof(SeriesView.IsNu), SeriesComparison.Equal, "true")
                ),
            ]
        ),
        new(
            "NU particles with a U-234 value",
            SeriesQueryEntity.Particles,
            [
                new(
                    SeriesQueryEntity.Particles,
                    new(nameof(ParticleView.IsNu), SeriesComparison.Equal, "true")
                ),
                new(
                    SeriesQueryEntity.Particles,
                    new(nameof(ParticleView.U234), SeriesComparison.IsNotNull, null)
                ),
            ]
        ),
        QueryBuilderExample.Advanced,
    ];

    [Parameter]
    public EventCallback OnAssistantViewChanged { get; set; }

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
    PresetFilterDropDown _presetDropDown = null!;

    PresetFilterBox _appliedFilterBox = new();
    PresetFilter _activeFilter = new();

    internal long AssistantDraftRevision { get; private set; }
    internal bool IsAssistantReady { get; private set; }

    public AssistantSurface AssistantResultSurface =>
        _activeGrid switch
        {
            SampleGrid => AssistantSurface.Samples,
            SubSampleGrid => AssistantSurface.SubSamples,
            ApmGrid => AssistantSurface.Apms,
            ParticleGrid => AssistantSurface.Particles,
            _ => AssistantSurface.Series,
        };

    IDataGridComponent[] _dataGrids =>
        [_seriesGrid, _sampleGrid, _subSampleGrid, _apmGrid, _particleGrid];

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _activeGrid = _seriesGrid;
            await _activeGrid.Reset();
            IsAssistantReady = true;
            await OnAssistantViewChanged.InvokeAsync();
            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    PresetFilter GetActivePresetFilter()
    {
        List<PresetFilterEntry> entries = [];
        foreach (IPresetFilterComponent component in PresetFilterComponents)
        {
            PresetFilterEntry entry = component.PresetFilterEntry;
            entry.IsEnabled = _activeFilter.EnsurePresetFilterEntry(component.EntryType).IsEnabled;
            if (entry.Descriptors.Any())
                entries.Add(entry);
        }
        _activeFilter.Entries = entries;

        return _activeFilter;
    }

    public PresetFilterBox GetPresetFilterBox() => _appliedFilterBox;

    public async Task<bool> ApplyAssistantAction(AssistantAction action)
    {
        if (_activeGrid is null)
            return false;

        if (action.Kind == AssistantActionKind.ShowQueryBuilderExample)
            return await LoadExampleAsync(QueryBuilderExample.Advanced);

        if (action.Kind == AssistantActionKind.DraftQuery)
        {
            if (
                action.BuilderFilters is null
                || CreateTypedDraft(action.BuilderFilters) is not { } draft
            )
                return false;

            await ApplyDraftAsync(draft);
            return true;
        }

        bool clear = action.Kind == AssistantActionKind.ClearGrid;
        if (!clear && action.Kind != AssistantActionKind.FilterGrid)
            return false;

        if (_activeGrid is SeriesGrid series)
        {
            if (!clear && action.SeriesQuery is null)
                return false;

            await series.SetAssistantQuery(clear ? null : action.SeriesQuery);
        }
        else
        {
            if (!clear && action.GridQuery?.Grid != AssistantResultSurface)
                return false;

            await _activeGrid.SetAssistantGridQuery(clear ? null : action.GridQuery);
        }
        return true;
    }

    Task ApplyDraftAsync(PresetFilter draft)
    {
        _activeFilter = draft;
        _presetDropDown.SetUnsavedDraft(draft);
        ReRenderPresetFilters(draft);
        _appliedFilterBox = BuildPresetFilterBox();
        return _activeGrid is SeriesGrid seriesGrid
            ? seriesGrid.SetAssistantQuery(null)
            : _activeGrid.SetAssistantGridQuery(null);
    }

    async Task<bool> LoadExampleAsync(QueryBuilderExample example)
    {
        IDataGridComponent? grid = example.ResultEntity switch
        {
            SeriesQueryEntity.Series => _seriesGrid,
            SeriesQueryEntity.Samples => _sampleGrid,
            SeriesQueryEntity.SubSamples => _subSampleGrid,
            SeriesQueryEntity.Apms => _apmGrid,
            SeriesQueryEntity.Particles => _particleGrid,
            _ => null,
        };
        if (grid is null || CreateTypedDraft(example.Filters) is not { } draft)
            return false;

        if (example == QueryBuilderExample.Advanced)
            draft.Name = example.Title;
        _activeGrid = grid;
        Task publish = NotifyDraftEdited();
        await ApplyDraftAsync(draft);
        await publish;
        return true;
    }

    static PresetFilter? CreateTypedDraft(IReadOnlyList<AssistantBuilderFilter> filters)
    {
        if (filters.Count > AssistantQueryBuilderFields.MaxFilters)
            return null;

        Dictionary<PresetFilterEntryType, List<CompositeFilterDescriptor>> entries = [];
        foreach (AssistantBuilderFilter item in filters)
        {
            if (
                item?.Filter is not { } filter
                || !AssistantQueryBuilderFields.IsAllowed(item.Entity, filter.Field)
                || GetAssistantEditor(item.Entity) is not { } editor
                || editor.View.GetProperty(filter.Field) is not { } property
                || !TryReadAssistantComparison(
                    property,
                    filter,
                    out FilterOperator comparison,
                    out object? value
                )
            )
                return null;

            if (
                !entries.TryGetValue(editor.Entry, out List<CompositeFilterDescriptor>? descriptors)
            )
                entries[editor.Entry] = descriptors = [];

            descriptors.Add(
                new CompositeFilterDescriptor
                {
                    Property = $"{editor.Entry}.{property.Name}",
                    FilterOperator = comparison,
                    FilterValue = value,
                    LogicalFilterOperator = LogicalFilterOperator.And,
                }
            );
        }

        return new PresetFilter
        {
            Entries =
            [
                .. entries.Select(entry => PresetFilterEntry.Create(entry.Key, entry.Value)),
            ],
        };
    }

    static (PresetFilterEntryType Entry, Type View)? GetAssistantEditor(SeriesQueryEntity entity) =>
        entity switch
        {
            SeriesQueryEntity.Series => (PresetFilterEntryType.Series, typeof(SeriesView)),
            SeriesQueryEntity.Samples => (PresetFilterEntryType.Sample, typeof(SampleView)),
            SeriesQueryEntity.SubSamples => (
                PresetFilterEntryType.SubSample,
                typeof(SubSampleView)
            ),
            SeriesQueryEntity.Apms => (PresetFilterEntryType.Apm, typeof(ApmView)),
            SeriesQueryEntity.Particles => (PresetFilterEntryType.Particle, typeof(ParticleView)),
            _ => null,
        };

    static bool TryReadAssistantComparison(
        PropertyInfo property,
        SeriesFieldFilter filter,
        out FilterOperator comparison,
        out object? value
    )
    {
        value = null;
        Type? nullableType = Nullable.GetUnderlyingType(property.PropertyType);
        Type type = nullableType ?? property.PropertyType;
        FilterOperator? supported = filter.Comparison switch
        {
            SeriesComparison.Equal => FilterOperator.Equals,
            SeriesComparison.NotEqual => FilterOperator.NotEquals,
            SeriesComparison.GreaterThan when IsOrdered(type) => FilterOperator.GreaterThan,
            SeriesComparison.GreaterOrEqual when IsOrdered(type) =>
                FilterOperator.GreaterThanOrEquals,
            SeriesComparison.LessThan when IsOrdered(type) => FilterOperator.LessThan,
            SeriesComparison.LessOrEqual when IsOrdered(type) => FilterOperator.LessThanOrEquals,
            SeriesComparison.Contains when type == typeof(string) => FilterOperator.Contains,
            SeriesComparison.StartsWith when type == typeof(string) => FilterOperator.StartsWith,
            SeriesComparison.EndsWith when type == typeof(string) => FilterOperator.EndsWith,
            SeriesComparison.IsNull when nullableType is not null || type == typeof(string) =>
                FilterOperator.IsNull,
            SeriesComparison.IsNotNull when nullableType is not null || type == typeof(string) =>
                FilterOperator.IsNotNull,
            _ => null,
        };
        comparison = supported.GetValueOrDefault();
        if (!supported.HasValue)
            return false;
        if (comparison is FilterOperator.IsNull or FilterOperator.IsNotNull)
            return filter.Value is null;
        if (filter.Value is not { Length: <= 250 } text)
            return false;
        if (type.IsEnum)
            return Enum.TryParse(type, text, true, out value)
                && value is not null
                && Enum.IsDefined(type, value);

        value = Type.GetTypeCode(type) switch
        {
            TypeCode.String => text,
            TypeCode.Boolean when bool.TryParse(text, out bool boolean) => boolean,
            TypeCode.Int32
                when int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int integer
                ) => integer,
            TypeCode.Decimal
                when decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal number
                ) => number,
            TypeCode.DateTime
                when DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime date
                ) => date,
            _ => null,
        };
        return value is not null;
    }

    static bool IsOrdered(Type type) =>
        type == typeof(int) || type == typeof(decimal) || type == typeof(DateTime);

    PresetFilterBox BuildPresetFilterBox()
    {
        PresetFilterBox presetFilterBox = new PresetFilterBox();
        foreach (IPresetFilterComponent filterComponent in PresetFilterComponents)
        {
            PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(
                filterComponent.EntryType
            );
            string? filterString = filterComponent.FilterString;
            if (filterEntry.IsEnabled && !string.IsNullOrWhiteSpace(filterString))
            {
                presetFilterBox.Set(filterComponent.EntryType, filterString);
            }
        }
        return presetFilterBox;
    }

    Task OnPresetFilterCheckBoxChanged(bool value, PresetFilterEntryType entryType)
    {
        PresetFilterEntry filterEntry = _activeFilter.EnsurePresetFilterEntry(entryType);
        filterEntry.IsEnabled = value;
        return NotifyDraftEdited();
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
        await NotifyDraftEdited();
        await ReloadActiveGrid();
    }

    void ReRenderPresetFilters(PresetFilter presetFilter)
    {
        Dictionary<PresetFilterEntryType, PresetFilterEntry> entryDict =
            presetFilter.Entries.ToDictionary(x => x.PresetFilterEntryType);

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
        await NotifyDraftEdited();
        await ReloadActiveGrid();
    }

    Task NotifyDraftEdited()
    {
        AssistantDraftRevision++;
        return OnAssistantViewChanged.InvokeAsync();
    }

    async Task ReloadActiveGrid()
    {
        if (_activeGrid is null)
        {
            return;
        }

        await JSRuntime.InvokeVoidAsync("forceUpdateNumericInputs");
        _appliedFilterBox = BuildPresetFilterBox();
        await _activeGrid.Reset(false, false);
    }

    async Task OnDataGridSelected(IDataGridComponent dataGrid)
    {
        _activeGrid = dataGrid;
        await OnAssistantViewChanged.InvokeAsync();
        await InvokeAsync(StateHasChanged);
        await dataGrid.Reset();
        await Task.Yield();
    }
}
