using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
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

    [Parameter]
    public EventCallback OnDraftEdited { get; set; }

    OperationMode _operationMode = OperationMode.Browsing;
    List<PresetFilter> _filters = [];
    ValidatedTextBox<PresetFilter> _validatedTextBoxRef = null!;
    PresetFilter _activeFilter = new();
    bool _savePending;

    int? ActiveFilterId => _activeFilter.Id == 0 ? null : _activeFilter.Id;

    string SubmitIcon =>
        _operationMode == OperationMode.Editing ? "check"
        : _activeFilter.Id == 0 ? "save"
        : "edit";

    string SubmitText =>
        _operationMode == OperationMode.Editing ? "Save"
        : _activeFilter.Id == 0 ? "Save preset"
        : "Edit preset";

    string SubmitTitle =>
        _operationMode == OperationMode.Editing ? "Save the current query builder filters."
        : _activeFilter.Id == 0 ? "Save the current query builder filters as a preset."
        : "Rename or update the selected saved filter.";

    string CancelIcon => _operationMode == OperationMode.Editing ? "close" : "filter_alt_off";

    string CancelText => _operationMode == OperationMode.Editing ? "Cancel" : "Clear preset";

    string CancelTitle =>
        _operationMode == OperationMode.Editing
            ? "Cancel editing this saved filter."
            : "Clear the selected saved filter and reload the results.";

    // Mirror PresetFilterValidator's length rule so the save button can be gated synchronously.
    const int MinNameLength = 5;
    const int MaxNameLength = 25;

    // The button must not enable on the stale "valid" state ReInitialize() leaves behind for a
    // blank name. The length check rejects that synchronously; IsValid covers the async uniqueness
    // rule once the name has been validated.
    bool CanSubmitFilter()
    {
        string name = _activeFilter.Name ?? string.Empty;
        return !_savePending
            && name.Length >= MinNameLength
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
        if (_savePending)
            return;

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
        if (!CanSubmitFilter())
            return;

        PresetFilter editor = _activeFilter;
        PresetFilter snapshot = CapturePreset(editor);

        bool IsCurrentEditor() =>
            ReferenceEquals(_activeFilter, editor)
            && _operationMode == OperationMode.Editing
            && editor.Name == snapshot.Name;

        _savePending = true;
        try
        {
            await NotifyDraftEdited();
            ValidationResult validationResult = await PresetFilterValidator.ValidateAsync(
                snapshot,
                options => options.IncludeProperties(nameof(PresetFilter.Name))
            );
            if (!validationResult.IsValid || !IsCurrentEditor())
                return;

            if (snapshot.Id == 0)
            {
                snapshot.Id = await Api.CreatePresetFilter(snapshot);
                _filters = [.. _filters, snapshot];
            }
            else
            {
                await Api.UpdatePresetFilter(snapshot);
                _filters = _filters
                    .Select(filter => filter.Id == snapshot.Id ? snapshot : filter)
                    .ToList();
            }

            if (IsCurrentEditor())
            {
                editor.Id = snapshot.Id;
                _validatedTextBoxRef.Commit();
                await ToggleOperationMode(OperationMode.Browsing);
            }
        }
        finally
        {
            _savePending = false;
        }
    }

    PresetFilter CapturePreset(PresetFilter editor) =>
        new()
        {
            Id = editor.Id,
            Name = editor.Name,
            Entries = GetAppliedPresetFilter()
                .Entries.Select(entry => new PresetFilterEntry
                {
                    Id = entry.Id,
                    PresetFilterId = entry.PresetFilterId,
                    PresetFilterEntryType = entry.PresetFilterEntryType,
                    LogicalFilterOperator = entry.LogicalFilterOperator,
                    IsEnabled = entry.IsEnabled,
                    SerializedDescriptors = entry.SerializedDescriptors,
                })
                .ToList(),
        };

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
        int id = _activeFilter.Id;
        if (id == 0)
            return;

        bool? result = await DialogService.Confirm(
            "Are you sure you want to delete this filter?",
            "Confirm Delete",
            new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
        );
        if (result == true)
            await DeleteFilter(id);
    }

    async Task DeleteFilter(int id)
    {
        await NotifyDraftEdited();
        await Api.DeletePresetFilter(id);
        _filters = _filters.Where(filter => filter.Id != id).ToList();
        if (_activeFilter.Id == id)
        {
            _activeFilter = new();
            await NotifyActiveFilterChanged();
        }
    }

    async Task ToggleOperationMode(OperationMode operationMode)
    {
        _operationMode = operationMode;
        await NotifyDraftEdited();

        if (operationMode == OperationMode.Editing)
        {
            _validatedTextBoxRef.ReInitialize();
            await _validatedTextBoxRef.FocusAsync();
            if (
                _operationMode == OperationMode.Editing
                && _activeFilter.Id == 0
                && !string.IsNullOrWhiteSpace(_activeFilter.Name)
            )
                await _validatedTextBoxRef.Validate();
        }
        else
        {
            _validatedTextBoxRef.CancelValidation();
        }
    }

    Task NotifyDraftEdited() => OnDraftEdited.InvokeAsync();

    async Task ClearSelectedFilter()
    {
        _activeFilter = new();
        await NotifyActiveFilterChanged();
    }

    internal void SetUnsavedDraft(PresetFilter draft)
    {
        if (_operationMode == OperationMode.Editing)
            _validatedTextBoxRef.CancelValidation();
        _operationMode = OperationMode.Browsing;
        _activeFilter = draft;
        StateHasChanged();
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
