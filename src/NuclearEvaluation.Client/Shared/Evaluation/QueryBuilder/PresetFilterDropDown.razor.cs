using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using FluentValidation;
using FluentValidation.Results;
using Radzen;

namespace NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;

public partial class PresetFilterDropDown : ComponentBase
{
    [Inject]
    public INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    public PresetFilterValidator PresetFilterValidator { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;

    [CascadingParameter]
    public Func<PresetFilter> GetAppliedPresetFilter { get; set; } = null!;

    [Parameter]
    public EventCallback<PresetFilter> OnPresetFilterSelected { get; set; }

    OperationMode _operationMode = OperationMode.Browsing;
    List<PresetFilter> _filters = [];
    ValidatedTextBox<PresetFilter> _validatedTextBoxRef = null!;
    PresetFilter _activeFilter = new();

    int? ActiveFilterId => _activeFilter.Id == 0 ? null : _activeFilter.Id;

    string SubmitIcon => _operationMode == OperationMode.Editing
        ? "check"
        : _activeFilter.Id == 0 ? "save" : "edit";

    string SubmitText => _operationMode == OperationMode.Editing
        ? "Save"
        : _activeFilter.Id == 0 ? "Save preset" : "Edit preset";

    string SubmitTitle => _operationMode == OperationMode.Editing
        ? "Save the current query builder filters."
        : _activeFilter.Id == 0
            ? "Save the current query builder filters as a preset."
            : "Rename or update the selected saved filter.";

    string CancelIcon => _operationMode == OperationMode.Editing ? "close" : "filter_alt_off";

    string CancelText => _operationMode == OperationMode.Editing ? "Cancel" : "Clear preset";

    string CancelTitle => _operationMode == OperationMode.Editing
        ? "Cancel editing this saved filter."
        : "Clear the selected saved filter and reload the results.";

    string ToolbarButtonStyle { get; } = "padding: 5px 10px; white-space: nowrap;";

    // Mirror PresetFilterValidator's length rule so the save button can be gated synchronously.
    const int MinNameLength = 5;
    const int MaxNameLength = 25;

    // The button must not enable on the stale "valid" state ReInitialize() leaves behind for a
    // blank name. The length check rejects that synchronously; IsValid covers the async uniqueness
    // rule once the name has been validated.
    bool CanSubmitFilter()
    {
        string name = _activeFilter.Name ?? string.Empty;
        return name.Length >= MinNameLength
            && name.Length <= MaxNameLength
            && (_validatedTextBoxRef?.IsValid ?? false);
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadFilters();
    }

    async Task LoadFilters()
    {
        _filters = await Api.GetPresetFilters();
    }

    async Task SubmitAction()
    {
        if (_operationMode == OperationMode.Editing)
        {
            await ProcessNewOrEditedFilter();
        }
        else
        {
            await ToggleOperationMode(OperationMode.Editing);
        }
    }

    async Task ProcessNewOrEditedFilter()
    {
        if (!_validatedTextBoxRef.IsValid)
        {
            return;
        }

        ValidationResult validationResult = await PresetFilterValidator
            .ValidateAsync(_activeFilter, options => options.IncludeProperties(nameof(PresetFilter.Name)));

        if (!validationResult.IsValid)
        {
            return;
        }

        if (_activeFilter.Id == 0)
        {
            await AddFilter();
        }
        else
        {
            await UpdateFilter();
        }

        _validatedTextBoxRef.Commit();
        await ToggleOperationMode(OperationMode.Browsing);
    }

    async Task AddFilter()
    {
        PresetFilter applied = GetAppliedPresetFilter();
        _activeFilter.Entries = applied.Entries;

        int id = await Api.CreatePresetFilter(_activeFilter);
        _activeFilter.Id = id;
        _filters.Add(_activeFilter);
    }

    async Task UpdateFilter()
    {
        PresetFilter applied = GetAppliedPresetFilter();
        _activeFilter.Entries = applied.Entries;

        await Api.UpdatePresetFilter(_activeFilter);
    }

    async Task CancelAction()
    {
        if (_operationMode == OperationMode.Editing)
        {
            await ToggleOperationMode(OperationMode.Browsing);
            return;
        }

        await ClearSelectedFilter();
    }

    async Task ConfirmDelete()
    {
        bool? result = await DialogService.Confirm("Are you sure you want to delete this filter?", "Confirm Delete", new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" });
        if (result.HasValue && result.Value)
        {
            await DeleteFilter();
            await NotifyActiveFilterChanged();
        }
    }

    async Task DeleteFilter()
    {
        if (_activeFilter.Id != 0)
        {
            await Api.DeletePresetFilter(_activeFilter.Id);
            _filters.Remove(_activeFilter);
            _activeFilter = new();
        }
    }

    async Task ToggleOperationMode(OperationMode operationMode)
    {
        _operationMode = operationMode;

        if (operationMode == OperationMode.Editing)
        {
            _validatedTextBoxRef.ReInitialize();
            await _validatedTextBoxRef.FocusAsync();
        }
        else
        {
            _validatedTextBoxRef.CancelValidation();
        }
    }

    async Task ClearSelectedFilter()
    {
        _activeFilter = new();
        await NotifyActiveFilterChanged();
    }

    async Task OnDropDownChange(int? value)
    {
        _activeFilter = _filters.FirstOrDefault(x => x.Id == value) ?? new();
        await NotifyActiveFilterChanged();
    }

    async Task NotifyActiveFilterChanged()
    {
        await OnPresetFilterSelected.InvokeAsync(_activeFilter);
        _validatedTextBoxRef.ReInitialize();
        await InvokeAsync(StateHasChanged);
    }
}
