using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Server.Interfaces.Cache;
using NuclearEvaluation.Server.Interfaces.Components;
using Radzen;

namespace NuclearEvaluation.Server.Shared.Grids;

public abstract class BaseGridGeneric<T> : ComponentBase, IDataGridComponent
{
    [Inject]
    protected ISessionCache SessionCache { get; set; } = null!;

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
    protected bool isLoading = false;
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

    protected async Task FetchData(Func<Task<FetchDataResult<T>>> fetchDataFunction)
    {
        FetchDataResult<T> result = await fetchDataFunction();

        if (result.IsSuccessful)
        {
            entries = result.Entries.ToList();
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
        //TODO use styles
        string fontSettings = "font-family: 'Material Symbols Outlined', 'Arial', sans-serif;";

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
};