using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiation;

public partial class RadNetBuilder
{
    const string PresetStorageKey = "NuclearEvaluation.RadNet.Presets.v1";
    const int MaxPresets = 24;

    static readonly RadNetEntity[] Entities = Enum.GetValues<RadNetEntity>();
    static readonly RadNetOperator[] Operators = Enum.GetValues<RadNetOperator>();

    readonly List<GroupDraft> _groups = [];
    List<SavedPreset> _presets = [];
    RadNetEntity _entity = RadNetEntity.Observation;
    RadNetEntity _newGroupEntity = RadNetEntity.Station;
    string? _sortField;
    bool _descending;
    RadNetQuery? _applied;
    int _resultRevision;
    string _selectedPreset = "";
    string _presetName = "";
    string? _notice;
    bool _editorCollapsed;

    [Inject]
    IJSRuntime Js { get; set; } = null!;

    [Parameter]
    public EventCallback<(RadNetEntity Entity, RadNetRow Row)> OnOpen { get; set; }

    [Parameter]
    public EventCallback<RadNetRow> OnQuality { get; set; }

    bool CanAddCondition =>
        _groups.Sum(group => group.Conditions.Count) < RadNetCatalog.MaxConditions;
    bool CanAddGroup => _groups.Count < RadNetCatalog.MaxGroups && CanAddCondition;

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
            if (stored is not null)
            {
                _presets = JsonSerializer
                    .Deserialize<List<SavedPreset>>(stored)
                    ?.Where(IsCompatiblePreset)
                    .Take(MaxPresets)
                    .ToList()
                    ?? [];
                StateHasChanged();
            }
        }
        catch (Exception error) when (error is JSException or JsonException)
        {
            _notice = "Saved presets could not be read from this browser.";
            StateHasChanged();
        }
    }

    void RunExample()
    {
        _selectedPreset = "";
        _presetName = RadNetExample.Title;
        SetDraft(RadNetExample.Query);
        SetAppliedQuery(RadNetExample.Query);
        _notice = null;
    }

    void ResetFilters()
    {
        RadNetQuery query = new(_entity);
        _selectedPreset = "";
        _presetName = "";
        SetDraft(query);
        SetAppliedQuery(query);
        _notice = null;
    }

    internal Task ApplyAssistantQueryAsync(RadNetQuery query)
    {
        _selectedPreset = "";
        _presetName = "";
        SetDraft(query);
        SetAppliedQuery(query);
        _notice = null;
        return InvokeAsync(StateHasChanged);
    }

    void AddGroup()
    {
        if (!CanAddGroup)
            return;

        GroupDraft group = new(_newGroupEntity);
        _groups.Add(group);
        group.Conditions.Add(new(DefaultField(group.Entity)));
    }

    void AddCondition(GroupDraft group)
    {
        if (CanAddCondition)
            group.Conditions.Add(new(DefaultField(group.Entity)));
    }

    void RemoveCondition(GroupDraft group, ConditionDraft condition)
    {
        group.Conditions.Remove(condition);
        if (group.Conditions.Count == 0)
            _groups.Remove(group);
    }

    static IEnumerable<RadNetField> Fields(RadNetEntity entity) =>
        RadNetCatalog.Fields.Where(field => field.Entity == entity);

    static RadNetField DefaultField(RadNetEntity entity) =>
        Fields(entity).First(field => !field.Name.EndsWith("Id", StringComparison.Ordinal));

    static void ChangeMatchMode(GroupDraft group, ChangeEventArgs args) =>
        group.MatchAny = args.Value?.ToString() == "any";

    static void ChangeField(RadNetEntity entity, ConditionDraft condition, string? name)
    {
        if (Fields(entity).FirstOrDefault(field => field.Name == name) is not { } field)
            return;

        condition.Field = field;
        condition.Operator = RadNetOperator.Equal;
        condition.Value = "";
    }

    static string InputType(RadNetFieldType type) =>
        type switch
        {
            RadNetFieldType.Integer or RadNetFieldType.Number => "number",
            RadNetFieldType.DateTime => "datetime-local",
            _ => "text",
        };

    static string? InputStep(RadNetFieldType type) =>
        type switch
        {
            RadNetFieldType.Number => "any",
            RadNetFieldType.Integer => "1",
            RadNetFieldType.DateTime => "1",
            _ => null,
        };

    static string Placeholder(RadNetField field) =>
        field.Name switch
        {
            nameof(RadNetRow.State) => "e.g. CA",
            nameof(RadNetRow.Channel) => "e.g. R03",
            nameof(RadNetRow.Status) => "e.g. APPROVED",
            _ => "Value",
        };

    void SetResultEntity(string? result)
    {
        if (Enum.TryParse(result, out RadNetEntity entity) && Enum.IsDefined(entity))
        {
            _entity = entity;
            _sortField = null;
            _descending = false;
        }
    }

    void SetSort((string Field, bool Descending) sort)
    {
        if (_applied?.Entity == _entity)
            (_sortField, _descending) = sort;
    }

    Task OpenRow(RadNetRow row) =>
        _applied is { } query ? OnOpen.InvokeAsync((query.Entity, row)) : Task.CompletedTask;

    void ApplyQuery()
    {
        if (BuildQuery() is { } query)
            SetAppliedQuery(query);
    }

    void SetAppliedQuery(RadNetQuery query)
    {
        _applied = query with { Skip = 0 };
        _resultRevision++;
    }

    RadNetQuery? BuildQuery()
    {
        _notice = null;
        List<RadNetFilterGroup> filters = [];
        foreach (GroupDraft group in _groups)
        {
            List<RadNetCondition> conditions = [];
            foreach (ConditionDraft condition in group.Conditions)
            {
                string? value = RadNetPresentation.TakesValue(condition.Operator)
                    ? condition.Value.Trim()
                    : null;
                if (value is { Length: 0 })
                {
                    _notice = $"Enter a value for {condition.Field.Label}.";
                    return null;
                }

                conditions.Add(new(condition.Field.Name, condition.Operator, value));
            }

            if (conditions.Count > 0)
                filters.Add(new(group.Entity, group.MatchAny, conditions));
        }

        return new(_entity, filters, _sortField, _descending);
    }

    void SetDraft(RadNetQuery query)
    {
        (_entity, _sortField, _descending) = (query.Entity, query.SortField, query.Descending);
        _groups.Clear();
        foreach (RadNetFilterGroup filter in query.Filters ?? [])
        {
            GroupDraft group = new(filter.Entity) { MatchAny = filter.MatchAny };
            foreach (RadNetCondition condition in filter.Conditions)
            {
                if (
                    Fields(filter.Entity)
                        .FirstOrDefault(item =>
                            item.Name.Equals(condition.Field, StringComparison.OrdinalIgnoreCase)
                        ) is
                    { } field
                )
                {
                    group.Conditions.Add(
                        new(field)
                        {
                            Operator = condition.Operator,
                            Value = FormatDraftValue(field, condition.Value),
                        }
                    );
                }
            }

            if (group.Conditions.Count > 0)
                _groups.Add(group);
        }
    }

    static string FormatDraftValue(RadNetField field, string? value) =>
        field.Type == RadNetFieldType.DateTime
        && DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset date
        )
            ? date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            : value ?? "";

    void LoadPreset(ChangeEventArgs args)
    {
        _selectedPreset = args.Value?.ToString() ?? "";
        if (_presets.Find(preset => preset.Name == _selectedPreset) is not { } selected)
            return;

        SetDraft(selected.Query);
        _presetName = selected.Name;
        _notice = null;
        SetAppliedQuery(selected.Query);
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
            || query.Top is < 1 or > RadNetCatalog.MaxPageSize
        )
            return false;

        IReadOnlyList<RadNetFilterGroup> filters = query.Filters ?? [];
        return filters.Count <= RadNetCatalog.MaxGroups
            && filters.All(IsCompatibleGroup)
            && filters.Sum(group => group.Conditions.Count) <= RadNetCatalog.MaxConditions;
    }

    static bool IsCompatibleGroup(RadNetFilterGroup group)
    {
        if (group is not { Conditions: { } conditions } || !Enum.IsDefined(group.Entity))
            return false;
        return conditions.All(condition =>
            condition is not null
            && Fields(group.Entity)
                .Any(field =>
                    field.Name == condition.Field
                    && RadNetCatalog.CanCompare(field, condition.Operator)
                )
        );
    }

    sealed class GroupDraft(RadNetEntity entity)
    {
        public RadNetEntity Entity { get; } = entity;
        public bool MatchAny { get; set; }
        public List<ConditionDraft> Conditions { get; } = [];
    }

    sealed class ConditionDraft(RadNetField field)
    {
        public RadNetField Field { get; set; } = field;
        public RadNetOperator Operator { get; set; }
        public string Value { get; set; } = "";
    }

    sealed record SavedPreset(string Name, RadNetQuery Query);
}
