using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Server.Validators;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Z.EntityFramework.Plus;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Server.Shared.Evaluation.QueryBuilder;

public partial class PresetFilterDropDown : ComponentBase
{
    [Inject]
    public NuclearEvaluationServerDbContext DbContext { get; set; } = null!;

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

    string _submitIcon = "save";
    string _cancelIcon = "clear_all";

    protected override async Task OnInitializedAsync()
    {
        await LoadFilters();
    }

    async Task LoadFilters()
    {
        _filters = await DbContext.PresetFilter
            .AsTracking()
            .IncludeOptimized(x => x.Entries)
            .OrderBy(x => x.Name)
            .ToListAsync();
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
        if (_validatedTextBoxRef.HasValidationErrors)
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
        PresetFilter filter = GetAppliedPresetFilter();
        _activeFilter.Entries = filter.Entries;

        _filters.Add(_activeFilter);
        DbContext.Add(_activeFilter);
        await DbContext.SaveChangesAsync();

        DbContext.Entry(_activeFilter).State = EntityState.Detached;
    }

    async Task UpdateFilter()
    {
        PresetFilter filter = GetAppliedPresetFilter();
        _activeFilter.Entries = filter.Entries;
        foreach (PresetFilterEntry entry in _activeFilter.Entries)
        {
            entry.PresetFilter = _activeFilter;
        }

        DbContext.Update(_activeFilter);
        await DbContext.SaveChangesAsync();
    }

    async Task CancelAction()
    {
        await ToggleOperationMode(OperationMode.Browsing);
        _activeFilter = new();
        await OnPresetFilterSelected.InvokeAsync(_activeFilter);
        _validatedTextBoxRef.CancelValidation();
    }

    async Task ConfirmDelete()
    {
        bool? result = await DialogService.Confirm("Are you sure you want to delete this filter?", "Confirm Delete", new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" });
        if (result.HasValue && result.Value)
        {
            await DeleteFilter();
            await OnDropDownChange();
        }
    }

    async Task DeleteFilter()
    {
        if (_activeFilter.Id != 0)
        {
            DbContext.Remove(_activeFilter);
            await DbContext.SaveChangesAsync();
            _filters.Remove(_activeFilter);
            _activeFilter = new();
        }
    }

    async Task ToggleOperationMode(OperationMode operationMode)
    {
        _operationMode = operationMode;

        if (operationMode == OperationMode.Editing)
        {
            _submitIcon = "check";

            _validatedTextBoxRef.ReInitialize();
            await _validatedTextBoxRef.FocusAsync();
        }
        else
        {
            _submitIcon = "save";
            _cancelIcon = "clear_all";
            _validatedTextBoxRef.CancelValidation();
        }
    }

    async Task OnDropDownChange()
    {
        await OnPresetFilterSelected.InvokeAsync(_activeFilter);
        _validatedTextBoxRef.ReInitialize();
    }
}