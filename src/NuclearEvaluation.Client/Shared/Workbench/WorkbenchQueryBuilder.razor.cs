using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiation.RadNetPresentation;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class WorkbenchQueryBuilder
{
    const string PresetStorageKey = "NuclearEvaluation.Workbench.QueryPresets.v1";
    const int MaxPresets = 24;
    static readonly WorkbenchQueryEntity[] Entities = Enum.GetValues<WorkbenchQueryEntity>();
    static readonly RadNetOperator[] Operators = Enum.GetValues<RadNetOperator>();

    readonly List<GroupDraft> _groups = [];
    List<SavedPreset> _presets = [];
    WorkbenchQueryEntity _entity = WorkbenchQueryEntity.Observation;
    WorkbenchQuery? _applied;
    string? _sortField;
    string? _notice;
    string _selectedPreset = "";
    string _presetName = "";
    int? _minimum;
    bool _descending;
    bool _openedExample;

    [Inject]
    IJSRuntime Js { get; set; } = null!;

    [Parameter]
    public bool RunExampleOnLoad { get; set; }

    bool CanAddCondition =>
        _groups.Sum(group => group.Conditions.Count) < WorkbenchQueryCatalog.MaxConditions;

    bool CanAddGroup => _groups.Count < WorkbenchQueryCatalog.MaxGroups && CanAddCondition;

    protected override void OnParametersSet()
    {
        if (RunExampleOnLoad && !_openedExample)
            RunExample();
        _openedExample = RunExampleOnLoad;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            string? stored = await Js.InvokeAsync<string?>(
                "localStorage.getItem",
                PresetStorageKey
            );
            if (stored is null)
                return;

            _presets = JsonSerializer
                .Deserialize<List<SavedPreset>>(stored)
                ?.Where(IsCompatiblePreset)
                .Take(MaxPresets)
                .ToList()
                ?? [];
        }
        catch (Exception error) when (error is JSException or JsonException)
        {
            _notice = "Saved presets could not be read from this browser.";
        }
        StateHasChanged();
    }

    static IEnumerable<WorkbenchQueryField> Fields(WorkbenchQueryEntity entity) =>
        WorkbenchQueryCatalog.Fields.Where(field => field.Entity == entity);

    static string LevelName(WorkbenchQueryEntity entity) =>
        entity == WorkbenchQueryEntity.File ? "Annual file" : entity.ToString();

    static string ResultName(WorkbenchQueryEntity entity) =>
        entity switch
        {
            WorkbenchQueryEntity.Project => "Projects",
            WorkbenchQueryEntity.Series => "Series",
            WorkbenchQueryEntity.File => "Annual files",
            WorkbenchQueryEntity.Observation => "Observations",
            _ => "Channel readings",
        };

    void AddGroup(WorkbenchQueryEntity level)
    {
        if (!CanAddGroup)
            return;

        GroupDraft group = new(level);
        AddCondition(group);
        _groups.Add(group);
    }

    void AddCondition(GroupDraft group)
    {
        if (CanAddCondition)
            group.Conditions.Add(new(Fields(group.Entity).First()));
    }

    void RemoveCondition(GroupDraft group, ConditionDraft condition)
    {
        group.Conditions.Remove(condition);
        if (group.Conditions.Count == 0)
            _groups.Remove(group);
    }

    static void ChangeField(ConditionDraft condition, string? name)
    {
        if (Fields(condition.Field.Entity).FirstOrDefault(field => field.Name == name) is { } field)
        {
            condition.Field = field;
            condition.Operator = RadNetOperator.Equal;
            condition.Value = "";
        }
    }

    static string InputType(WorkbenchQueryField field) =>
        field.Type switch
        {
            RadNetFieldType.Integer or RadNetFieldType.Number => "number",
            RadNetFieldType.DateTime when field.Entity == WorkbenchQueryEntity.Series => "date",
            RadNetFieldType.DateTime => "datetime-local",
            _ => "text",
        };

    static string? InputStep(WorkbenchQueryField field) =>
        field.Type switch
        {
            RadNetFieldType.Number => "any",
            RadNetFieldType.Integer or RadNetFieldType.DateTime => "1",
            _ => null,
        };

    static string FormatDraftValue(WorkbenchQueryField field, string? value)
    {
        if (
            field.Type == RadNetFieldType.DateTime
            && DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset date
            )
        )
        {
            string format =
                field.Entity == WorkbenchQueryEntity.Series
                    ? "yyyy-MM-dd"
                    : "yyyy-MM-dd'T'HH:mm:ss";
            return date.UtcDateTime.ToString(format, CultureInfo.InvariantCulture);
        }
        return value ?? "";
    }

    void ResetSort() => (_sortField, _descending) = (null, false);

    void SetSort((string Field, bool Descending) sort)
    {
        if (_applied?.Entity == _entity)
            (_sortField, _descending) = sort;
    }

    void ApplyQuery()
    {
        if (BuildQuery() is { } query)
            Run(query);
    }

    void RunExample()
    {
        _selectedPreset = "";
        _presetName = WorkbenchQueryExample.Title;
        SetDraft(WorkbenchQueryExample.Query);
        Run(WorkbenchQueryExample.Query);
    }

    void Run(WorkbenchQuery query)
    {
        _notice = null;
        _applied = query with { Skip = 0, ResultFilters = null, ResultFilterGroups = null };
    }

    WorkbenchQuery? BuildQuery()
    {
        if (_minimum is <= 0)
        {
            _notice = "Enter a positive minimum, or leave it blank.";
            return null;
        }

        List<WorkbenchQueryFilterGroup> filters = [];
        foreach (GroupDraft group in _groups)
        {
            List<WorkbenchQueryCondition> conditions = [];
            foreach (ConditionDraft condition in group.Conditions)
            {
                string? value = TakesValue(condition.Operator) ? condition.Value.Trim() : null;
                if (value is { Length: 0 })
                {
                    _notice =
                        $"Enter a value for {condition.Field.Label}, or remove that condition.";
                    return null;
                }
                conditions.Add(new(condition.Field.Name, condition.Operator, value));
            }
            filters.Add(new(group.Entity, group.MatchMode == "any", conditions));
        }
        return new(_entity, filters, _sortField, _descending, _minimum);
    }

    void SetDraft(WorkbenchQuery query)
    {
        (_entity, _sortField, _descending, _minimum) = (
            query.Entity,
            query.SortField,
            query.Descending,
            query.MinimumMatchingObservations
        );
        _groups.Clear();
        foreach (WorkbenchQueryFilterGroup filter in query.Filters ?? [])
        {
            GroupDraft group = new(filter.Entity) { MatchMode = filter.MatchAny ? "any" : "all" };
            foreach (WorkbenchQueryCondition condition in filter.Conditions)
            {
                WorkbenchQueryField field = Fields(filter.Entity)
                    .First(item => item.Name == condition.Field);
                group.Conditions.Add(
                    new(field)
                    {
                        Operator = condition.Operator,
                        Value = FormatDraftValue(field, condition.Value),
                    }
                );
            }
            _groups.Add(group);
        }
    }

    void LoadPreset(ChangeEventArgs args)
    {
        _selectedPreset = args.Value?.ToString() ?? "";
        if (_presets.Find(preset => preset.Name == _selectedPreset) is { } preset)
        {
            _presetName = preset.Name;
            SetDraft(preset.Query);
            Run(preset.Query);
        }
    }

    async Task SavePresetAsync()
    {
        string name = _presetName.Trim();
        if (name.Length is 0 or > 80)
        {
            _notice = "Enter a preset name.";
            return;
        }
        if (BuildQuery() is not { } query)
            return;

        List<SavedPreset> next = _presets
            .Where(preset => !string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (next.Count >= MaxPresets)
        {
            _notice = "Delete a saved preset to make room for another.";
            return;
        }
        next.Add(new(name, query));
        if (await StorePresetsAsync(next))
        {
            _selectedPreset = name;
            _notice = $"Saved {name}.";
        }
    }

    async Task RemovePresetAsync()
    {
        List<SavedPreset> next = _presets.Where(preset => preset.Name != _selectedPreset).ToList();
        if (await StorePresetsAsync(next))
        {
            _selectedPreset = "";
            _presetName = "";
            _notice = "Preset deleted.";
        }
    }

    async Task<bool> StorePresetsAsync(List<SavedPreset> presets)
    {
        try
        {
            await Js.InvokeVoidAsync(
                "localStorage.setItem",
                PresetStorageKey,
                JsonSerializer.Serialize(presets)
            );
            _presets = presets;
            return true;
        }
        catch (JSException)
        {
            _notice = "This browser could not save presets.";
            return false;
        }
    }

    static bool IsCompatiblePreset(SavedPreset preset)
    {
        if (
            preset is not { Name.Length: > 0 and <= 80, Query: { } query }
            || !Enum.IsDefined(query.Entity)
            || query.Top is < 1 or > WorkbenchQueryCatalog.MaxPageSize
            || query.MinimumMatchingObservations is <= 0
            || (
                query.SortField is not null
                && !WorkbenchQueryCatalog.ResultFields.Any(field =>
                    field.Entity == query.Entity
                    && field.Name.Equals(query.SortField, StringComparison.OrdinalIgnoreCase)
                )
            )
        )
            return false;

        IReadOnlyList<WorkbenchQueryFilterGroup> filters = query.Filters ?? [];
        return filters.Count <= WorkbenchQueryCatalog.MaxGroups
            && filters.All(IsCompatibleGroup)
            && filters.Sum(group => group.Conditions.Count) <= WorkbenchQueryCatalog.MaxConditions;
    }

    static bool IsCompatibleGroup(WorkbenchQueryFilterGroup group) =>
        group is { Conditions.Count: > 0 }
        && Enum.IsDefined(group.Entity)
        && group.Conditions.All(condition =>
            condition is not null
            && Fields(group.Entity)
                .Any(field =>
                    field.Name == condition.Field
                    && WorkbenchQueryCatalog.CanCompare(field, condition.Operator)
                )
        );

    sealed class GroupDraft(WorkbenchQueryEntity entity)
    {
        public WorkbenchQueryEntity Entity { get; } = entity;
        public string MatchMode { get; set; } = "all";
        public List<ConditionDraft> Conditions { get; } = [];
    }

    sealed class ConditionDraft(WorkbenchQueryField field)
    {
        public WorkbenchQueryField Field { get; set; } = field;
        public RadNetOperator Operator { get; set; } = RadNetOperator.Equal;
        public string Value { get; set; } = "";
    }

    sealed record SavedPreset(string Name, WorkbenchQuery Query);
}
