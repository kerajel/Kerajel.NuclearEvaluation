using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Filters;
using Radzen;
using System.Text.Json;

namespace NuclearEvaluation.Client.Shared.Grids;

public abstract class BaseGridGeneric<T> : ComponentBase, IDataGridComponent
{
    [Inject]
    protected ISessionCache SessionCache { get; set; } = null!;

    [Inject]
    protected IGridResultCache ResultCache { get; set; } = null!;

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    [Parameter]
    public bool AllowEdit { get; set; } = true;

    [Parameter]
    public string ComponentId { get; set; } = Guid.NewGuid().ToString();

    [Parameter]
    public bool AllowExpand { get; set; } = true;

    [Parameter]
    public bool AllowSelect { get; set; } = false;

    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public Func<PresetFilterBox>? GetPresetFilterBox { get; set; }

    protected int totalCount = 0;
    protected List<T> entries = [];
    protected bool hasFetchDataError = false;
    // Start in the loading state so the first paint shows a loading indicator rather than a
    // misleading "No records" empty template before the initial async fetch completes.
    protected bool isLoading = true;
    protected DataGridSettings? dataGridSettings;

    protected virtual string DecimalFormat => "{0:0.##}";
    protected virtual string DateOnlyFormat => "{0:yyyy-MM-dd}";

    protected string GridSettingsKey => $"{ComponentId}_{nameof(DataGridSettings)}";

    protected DataGridSettings? GridSettings
    {
        get
        {
            if (dataGridSettings != null)
            {
                return dataGridSettings;
            }
            bool hasSettings = SessionCache.TryGetValue(GridSettingsKey, out DataGridSettings? settings);
            return hasSettings ? settings : new DataGridSettings();
        }
        set
        {
            dataGridSettings = value;
            SessionCache.Add(GridSettingsKey, dataGridSettings);
        }
    }

    public abstract string EntityDisplayName { get; }

    public abstract Task LoadData(LoadDataArgs args);

    public abstract Task Reset(bool resetColumnState = true, bool resetRowState = false);

    int _loadSequence;

    protected async Task FetchData(Func<Task<DataResult<T>>> fetchDataFunction)
    {
        ApplyResult(await fetchDataFunction());
    }

    /// <summary>
    /// Query-aware fetch with caching. If a result for this exact grid+query was seen before,
    /// it is shown immediately and a fresh fetch runs in the background; otherwise the grid
    /// loads normally. A sequence guard ensures a slow background refresh can never overwrite
    /// a newer query's results.
    /// </summary>
    protected async Task FetchData(DataQuery query, Func<Task<DataResult<T>>> fetchDataFunction)
    {
        int sequence = ++_loadSequence;
        string key = $"{GetType().Name}|{ComponentId}|{JsonSerializer.Serialize(query)}";

        if (ResultCache.TryGet(key, out List<T> cachedEntries, out int cachedTotal))
        {
            entries = cachedEntries;
            totalCount = cachedTotal;
            hasFetchDataError = false;
            isLoading = false;
            _ = RefreshInBackground(sequence, key, fetchDataFunction);
            return;
        }

        isLoading = true;
        await FetchAndApply(sequence, key, fetchDataFunction);
    }

    async Task RefreshInBackground(int sequence, string key, Func<Task<DataResult<T>>> fetchDataFunction)
    {
        await FetchAndApply(sequence, key, fetchDataFunction);
        if (sequence == _loadSequence)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    async Task FetchAndApply(int sequence, string key, Func<Task<DataResult<T>>> fetchDataFunction)
    {
        DataResult<T> result = await fetchDataFunction();

        // A newer LoadData superseded this fetch; discard its (now stale) result.
        if (sequence != _loadSequence)
        {
            return;
        }

        ApplyResult(result);

        if (result.IsSuccessful)
        {
            ResultCache.Set(key, entries, totalCount);
        }
    }

    void ApplyResult(DataResult<T> result)
    {
        if (result.IsSuccessful)
        {
            entries = [.. result.Entries];
            totalCount = result.TotalCount;
            hasFetchDataError = false;
        }
        else
        {
            entries = [];
            totalCount = 0;
            hasFetchDataError = true;
        }
    }

    protected RenderFragment EmptyTemplate => builder =>
    {
        const string fontSettings = "font-family: 'Material Symbols Outlined', 'Arial', sans-serif;";

        if (hasFetchDataError)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "style", $"color: darkorange; {fontSettings}");
            builder.AddContent(2, "An error occurred while fetching entries");
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "style", fontSettings);
            builder.AddContent(5, "No records to display");
            builder.CloseElement();
        }
    };
}
