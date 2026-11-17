using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Shared.Radiation;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiochemistry;

public partial class RemDbBuilder
{
    const string StorageKey = "NuclearEvaluation.RemDb.Presets.v1";
    const int MaxPresets = 24;
    static readonly RemDbEntity[] Entities = Enum.GetValues<RemDbEntity>();
    static readonly RadNetOperator[] Operators = Enum.GetValues<RadNetOperator>();

    readonly List<GroupDraft> _groups = [];
    List<SavedPreset> _presets = [];
    RemDbEntity _entity = RemDbEntity.Measurement;
    RemDbEntity _newGroup = RemDbEntity.Site;
    RemDbQuery? _applied;
    string? _sortField;
    string? _notice;
    string _selectedPreset = "";
    string _presetName = "";
    int _pageSize = 50;
    bool _descending;
    bool _exampleLoaded;

    [Inject]
    IJSRuntime Js { get; set; } = null!;

    [Parameter]
    public EventCallback<(RemDbEntity Entity, RemDbRow Row)> OnOpen { get; set; }

    [Parameter]
    public bool RunExampleOnLoad { get; set; }

    bool CanAddCondition =>
        _groups.Sum(group => group.Conditions.Count) < RemDbCatalog.MaxConditions;
    bool CanAddGroup => _groups.Count < RemDbCatalog.MaxGroups && CanAddCondition;

    protected override void OnParametersSet()
    {
        if (RunExampleOnLoad && !_exampleLoaded)
            RunExample();

        _exampleLoaded = RunExampleOnLoad;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            string? stored = await Js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
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

    void RunExample() =>
        LoadAndRun(RemDbExample.Query, RemDbExample.Title, $"Loaded {RemDbExample.Title}.");

    internal Task ApplyAssistantQueryAsync(RemDbQuery query)
    {
        LoadAndRun(query, "", null);
        return InvokeAsync(StateHasChanged);
    }

    void LoadAndRun(RemDbQuery query, string name, string? notice)
    {
        _selectedPreset = "";
        _presetName = name;
        _notice = notice;
        SetDraft(query);
        _applied = query with { Skip = 0 };
    }

    void AddGroup()
    {
        if (!CanAddGroup)
            return;

        GroupDraft group = new(_newGroup);
        _groups.Add(group);
        AddCondition(group);
    }

    void AddCondition(GroupDraft group)
    {
        if (CanAddCondition)
            group.Conditions.Add(
                new(
                    Fields(group.Entity)
                        .First(field => !field.Key.EndsWith("Id", StringComparison.Ordinal))
                )
            );
    }

    void RemoveCondition(GroupDraft group, ConditionDraft condition)
    {
        group.Conditions.Remove(condition);
        if (group.Conditions.Count == 0)
            _groups.Remove(group);
    }

    static IEnumerable<RemDbField> Fields(RemDbEntity entity) =>
        RemDbCatalog.Fields.Where(field => field.Entity == entity);

    static RemDbField? FindField(RemDbEntity entity, string? key) =>
        Fields(entity)
            .FirstOrDefault(field =>
                string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)
            );

    static void ChooseField(RemDbEntity entity, ConditionDraft condition, string? key)
    {
        if (FindField(entity, key) is not { } field)
            return;

        condition.Field = field;
        condition.Operator = RadNetOperator.Equal;
        condition.Value = "";
    }

    static string InputType(RadNetFieldType type) =>
        type switch
        {
            RadNetFieldType.DateTime => "date",
            RadNetFieldType.Integer or RadNetFieldType.Number => "number",
            _ => "text",
        };

    static string? InputStep(RadNetFieldType type) =>
        type switch
        {
            RadNetFieldType.Integer => "1",
            RadNetFieldType.Number => "any",
            _ => null,
        };

    static string Placeholder(RemDbField field) =>
        field.Key switch
        {
            nameof(RemDbRow.Country) => "e.g. Austria",
            nameof(RemDbRow.Nuclide) => "e.g. CS-137",
            nameof(RemDbRow.Medium) => "e.g. MILK COW",
            nameof(RemDbRow.Unit) => "e.g. BQ/M3",
            nameof(RemDbRow.Qualifier) => "e.g. <",
            _ => "Value",
        };

    void ResetSort() => (_sortField, _descending) = (null, false);

    void SetSort((string Field, bool Descending) sort)
    {
        if (_applied?.Entity == _entity)
            (_sortField, _descending) = sort;
    }

    Task OpenRow(RemDbRow row) =>
        _applied is { } query ? OnOpen.InvokeAsync((query.Entity, row)) : Task.CompletedTask;

    void RunQuery()
    {
        if (BuildQuery() is { } query)
            _applied = query;
    }

    RemDbQuery? BuildQuery()
    {
        _notice = null;
        List<RemDbFilterGroup> filters = [];
        foreach (GroupDraft group in _groups)
        {
            List<RemDbCondition> conditions = [];
            foreach (ConditionDraft condition in group.Conditions)
            {
                string? value = RadNetPresentation.TakesValue(condition.Operator)
                    ? condition.Value.Trim()
                    : null;
                if (value is not null && !ValidValue(condition.Field, value))
                {
                    _notice = $"Enter a valid value for {condition.Field.Label}.";
                    return null;
                }
                conditions.Add(new(condition.Field.Key, condition.Operator, value));
            }

            filters.Add(new(group.Entity, group.Match == MatchMode.Any, conditions));
        }

        return new(_entity, filters, _sortField, _descending, Top: _pageSize);
    }

    void SetDraft(RemDbQuery query)
    {
        (_entity, _sortField, _descending, _pageSize) = (
            query.Entity,
            query.SortField,
            query.Descending,
            query.Top
        );
        _groups.Clear();
        foreach (RemDbFilterGroup filter in query.Filters ?? [])
        {
            GroupDraft group = new(filter.Entity)
            {
                Match = filter.MatchAny ? MatchMode.Any : MatchMode.All,
            };
            foreach (RemDbCondition condition in filter.Conditions)
            {
                if (FindField(filter.Entity, condition.Field) is { } field)
                    group.Conditions.Add(
                        new(field) { Operator = condition.Operator, Value = condition.Value ?? "" }
                    );
            }
            if (group.Conditions.Count > 0)
                _groups.Add(group);
        }
    }

    void LoadPreset(ChangeEventArgs args)
    {
        string name = args.Value?.ToString() ?? "";
        if (_presets.Find(preset => preset.Name == name) is { } saved)
            LoadAndRun(saved.Query, saved.Name, null);
        _selectedPreset = name;
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
            .Where(item => !string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
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

    async Task DeletePresetAsync()
    {
        List<SavedPreset> next = _presets.Where(item => item.Name != _selectedPreset).ToList();
        if (await StorePresetsAsync(next))
        {
            _selectedPreset = "";
            _presetName = "";
            _notice = "Preset deleted.";
        }
    }

    async Task<bool> StorePresetsAsync(List<SavedPreset> next)
    {
        try
        {
            await Js.InvokeVoidAsync(
                "localStorage.setItem",
                StorageKey,
                JsonSerializer.Serialize(next)
            );
            _presets = next;
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
            || query.Top is < 1 or > RemDbCatalog.MaxPageSize
        )
            return false;

        IReadOnlyList<RemDbFilterGroup> filters = query.Filters ?? [];
        if (
            filters.Count > RemDbCatalog.MaxGroups
            || filters.Any(group => group?.Conditions is not { Count: > 0 })
            || filters.Sum(group => group.Conditions.Count) > RemDbCatalog.MaxConditions
        )
            return false;

        return filters.All(group =>
            group.Conditions.All(condition =>
                condition is not null
                && FindField(group.Entity, condition.Field) is { } field
                && RemDbCatalog.CanCompare(field, condition.Operator)
                && (
                    !RadNetPresentation.TakesValue(condition.Operator)
                    || ValidValue(field, condition.Value)
                )
            )
        );
    }

    static bool ValidValue(RemDbField field, string? value)
    {
        if (value is not { Length: > 0 and <= RemDbPresentation.MaxFilterValueLength })
            return false;

        return field.Type switch
        {
            RadNetFieldType.Integer => long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _
            ),
            RadNetFieldType.Number => double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number
            ) && double.IsFinite(number),
            RadNetFieldType.DateTime => DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            ),
            _ => true,
        };
    }

    enum MatchMode
    {
        All,
        Any,
    }

    sealed record SavedPreset(string Name, RemDbQuery Query);

    sealed class GroupDraft(RemDbEntity entity)
    {
        public RemDbEntity Entity { get; } = entity;
        public MatchMode Match { get; set; }
        public List<ConditionDraft> Conditions { get; } = [];
    }

    sealed class ConditionDraft(RemDbField field)
    {
        public RemDbField Field { get; set; } = field;
        public RadNetOperator Operator { get; set; }
        public string Value { get; set; } = "";
    }
}
