using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Generics;

public partial class GenericEnumColumnFilter<T, K>
    where T : struct, Enum
    where K : class
{
    [Inject]
    public INuclearEvaluationApi Api { get; set; } = null!;

    /// <summary>API entity slug whose distinct values populate the filter (e.g. "series", "samples").</summary>
    [Parameter]
    public string Entity { get; set; } = string.Empty;

    [Parameter]
    public DataQuery? Query { get; set; }

    [Parameter]
    public string PropertyName { get; set; } = string.Empty;

    [Parameter]
    public RadzenDataGridColumn<K>? Column { get; set; }

    protected IEnumerable<T> _selectedItems = Enumerable.Empty<T>();
    protected IEnumerable<T> _availableItems = Enumerable.Empty<T>();
    protected bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        IEnumerable<T> items;

        if (Query is not null && !string.IsNullOrEmpty(Entity))
        {
            _isLoading = true;
            StateHasChanged();
            await Task.Yield();

            EnumFilterRequest request = new() { PropertyName = PropertyName, Query = Query };
            List<int> options = await Api.GetEnumFilterOptions(Entity, request);
            items = options.Select(x => (T)Enum.ToObject(typeof(T), x));
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
