using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace NuclearEvaluation.Server.Shared.Generics;

public partial class GenericEnumColumnFilter<T, K>
    where T : struct, Enum
    where K : class
{
    [Inject]
    public IGenericService GenericService { get; set; } = null!;

    [Parameter]
    public FilterDataCommand<K>? Command { get; set; }

    [Parameter]
    public string PropertyName { get; set; } = string.Empty;

    [Parameter]
    public RadzenDataGridColumn<K>? Column { get; set; }

    protected IEnumerable<T> _selectedItems = Enumerable.Empty<T>();
    protected IEnumerable<T> _availableItems = Enumerable.Empty<T>();
    protected bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        IEnumerable<T> items = Enumerable.Empty<T>();

        if (Command != null)
        {
            _isLoading = true;

            StateHasChanged();
            await Task.Yield();

            FilterDataResponse<dynamic> filterResponse = await GenericService.GetFilterOptions(Command, PropertyName);

            items = filterResponse.Entries.Select(x => (T)x).ToArray();
        }
        else
        {
            items = Enum.GetValues<T>().Cast<T>();
        }

        _availableItems = [.. items.Order()];

        _isLoading = false;

        StateHasChanged();

        await base.OnInitializedAsync();
    }

    Task Clear()
    {
        _selectedItems = [];
        StateHasChanged();
        return Task.CompletedTask;
    }

    Task OnClear()
    {
        return Clear();
    }

    async Task OnApply()
    {
        string? whereExpression = _selectedItems?.Any() == true
            ? string.Join($" {Enum.GetName(LogicalFilterOperator.Or)} ",
              _selectedItems.Select(s => $"{PropertyName} == {(int)(object)s}"))
            : default;

        whereExpression = string.IsNullOrWhiteSpace(whereExpression) ? default : $" ({whereExpression}) ";

        await Column!.SetCustomFilterExpressionAsync(whereExpression);
    }
}