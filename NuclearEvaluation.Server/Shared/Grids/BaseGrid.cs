using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Filters;
using Radzen;

namespace NuclearEvaluation.Server.Shared.Grids;

public abstract class BaseGrid : ComponentBase, IDataGrid
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

    protected int totalCount;
    protected bool isLoading;
    protected DataGridSettings? dataGridSettings;

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
}