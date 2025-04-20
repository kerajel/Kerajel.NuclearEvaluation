using FluentValidation;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using System.Linq.Expressions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components.Web;
using System.Reflection;
using Radzen;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Helpers;

namespace NuclearEvaluation.Server.Shared.Generics;

public class ValidatedTextControlBase<TModel, K> : ComponentBase
{
    [Parameter]
    public TModel Model { get; set; } = default!;

    [Parameter]
    public K? Type { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Parameter]
    public Expression<Func<TModel, K?>> PropertyExpression { get; set; } = default!;

    [Parameter]
    public IValidator<TModel> Validator { get; set; } = default!;

    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public string Placeholder { get; set; } = string.Empty;

    [Parameter]
    public AutoCompleteType AutoCompleteType { get; set; } = AutoCompleteType.Off;

    [Parameter]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public string Style { get; set; } = string.Empty;

    [Parameter]
    public TimeSpan DebounceTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

    [Parameter]
    public int Rows { get; set; }

    [Parameter]
    public EventCallback<FocusEventArgs> OnBlur { get; set; }

    [Parameter]
    public EventCallback<bool> OnValidationStateChanged { get; set; }

    [Parameter]
    public decimal TooltipOffsetX { get; set; } = 0;

    [Parameter]
    public decimal TooltipOffsetY { get; set; } = 0;

    public IRadzenFormComponent _inputRef = null!;

    protected string? _validationMessage;

    protected string TooltipOffsetXpx => $"{TooltipOffsetX}px";
    protected string TooltipOffsetYpx => $"{TooltipOffsetY}px";
    protected string ComputedStyle => $"{Style}; border: 2px solid {(!IsValid ? "orange" : "transparent")};";

    protected Debouncer<ValidationResult> _validationDebounce = null!;

    protected K? _initialValue = default;
    protected K? _boundValue = default;

    protected PropertyInfo _propertyInfo = null!;
    protected Func<TModel, K?> _getter = null!;

    protected override void OnInitialized()
    {
        ReInitialize();
    }

    public void ReInitialize()
    {
        _validationDebounce = new Debouncer<ValidationResult>(DebounceTimeout);

        _propertyInfo = PropertyExpression.GetPropertyInfo();

        _getter = PropertyExpression.Compile();

        _initialValue = PropertyValue;
        _boundValue = _initialValue;
    }

    public bool ValueHasChanged()
    {
        if (_initialValue == null && PropertyValue == null)
        {
            return false;
        }

        if (_initialValue == null || PropertyValue == null)
        {
            return true;
        }

        return !_initialValue.Equals(PropertyValue);
    }

    public bool IsReadyToCommit()
    {
        if (!ValueHasChanged())
        {
            return false;
        }

        return IsValid;
    }

    public async Task FocusAsync()
    {
        StateHasChanged();
        await Task.Yield();

        if (_inputRef != null && Visible)
        {
            await _inputRef.FocusAsync();
        }
    }

    public async Task OnInput(ChangeEventArgs e)
    {
        object? value = e.Value is null ? default : Convert.ChangeType(e.Value, typeof(K));

        SetPropertyValue((K?)value);
        await Validate();
    }

    protected async Task OnValueChanged(K? newValue)
    {
        _boundValue = newValue;
        SetPropertyValue(newValue);
        _ = Validate();
    }

    public string PropertyName => _propertyInfo.Name;

    public K? PropertyValue
    {
        get => _getter(Model) ?? default;
        set
        {
            SetPropertyValue(value);
        }
    }

    public void CancelValidation()
    {
        IsValid = false;
        _validationDebounce.Cancel();
        PropertyValue = _initialValue ?? default;
        _boundValue = _initialValue;
        _validationMessage = string.Empty;
        StateHasChanged();
    }

    public bool IsValid { get; private set; } = true;

    public void Commit()
    {
        _initialValue = PropertyValue;
        StateHasChanged();
    }

    public async Task CancelChanges()
    {
        if (_initialValue != null)
        {
            SetPropertyValue(_initialValue);
            _initialValue = default;
            _boundValue = default;
            await Task.Yield();
        }
    }

    public async Task HandleOnBlur(FocusEventArgs e)
    {
        if (OnBlur.HasDelegate)
        {
            await OnBlur.InvokeAsync(e);
        }
    }

    void SetPropertyValue(K? value)
    {
        _propertyInfo.SetValue(Model, value);
        StateHasChanged();
    }

    public async Task<ValidationResult> Validate()
    {
        return await _validationDebounce.ExecuteAsync(Validate);

        async Task<ValidationResult> Validate()
        {
            bool previousIsValid = IsValid;

            ValidationResult validationResult = await Validator.ValidateAsync(Model, options => options.IncludeProperties(PropertyName));

            if (validationResult.IsValid)
            {
                IsValid = true;
                _validationMessage = string.Empty;
            }
            else
            {
                if (validationResult.Errors.Count > 0)
                {
                    _validationMessage = string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage));
                    IsValid = false;
                }
                else
                {
                    IsValid = true;
                    _validationMessage = string.Empty;
                }
            }

            if (previousIsValid != IsValid)
            {
                await InvokeAsync(() => OnValidationStateChanged.InvokeAsync(IsValid));
            }

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            return validationResult;
        }
    }
}