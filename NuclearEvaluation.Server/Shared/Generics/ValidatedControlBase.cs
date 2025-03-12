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

public class ValidatedControlBase<TModel> : ComponentBase
{
    [Parameter]
    public TModel Model { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Parameter]
    public Expression<Func<TModel, string>> PropertyExpression { get; set; } = default!;

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
    public int TooltipOffsetX { get; set; } = 0;

    [Parameter]
    public int TooltipOffsetY { get; set; } = 0;

    public FormComponent<string> _textInputRef = null!;

    protected bool _hasValidationErrors;
    protected string? _validationMessage;

    protected string TooltipOffsetXpx => $"{TooltipOffsetX}px";
    protected string TooltipOffsetYpx => $"{TooltipOffsetY}px";
    protected string ComputedStyle => $"{Style}; border: 2px solid {(_hasValidationErrors ? "orange" : "transparent")};";

    protected Debouncer<ValidationResult> _validationDebounce = null!;

    protected string? _initialValue = null;
    protected string? _boundValue = null;

    protected PropertyInfo _propertyInfo = null!;
    protected Func<TModel, string> _getter = null!;

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
        return _initialValue != PropertyValue;
    }

    public async Task<bool> IsReadyToCommit()
    {
        if (!ValueHasChanged())
        {
            return false;
        }

        ValidationResult validationResult = await Validator
            .ValidateAsync(Model, options => options.IncludeProperties(PropertyName));

        return validationResult.IsValid;
    }

    public async Task FocusAsync()
    {
        StateHasChanged();
        await Task.Yield();

        if (_textInputRef != null && Visible)
        {
            await _textInputRef.Element.FocusAsync();
        }
    }

    public async Task OnTextBoxInput(ChangeEventArgs e)
    {
        string value = e.Value?.ToString() ?? string.Empty;

        SetPropertyValue(value);
        await ValidateWithDebounce();
    }

    public string PropertyName => _propertyInfo.Name;

    public string PropertyValue
    {
        get => _getter(Model) ?? string.Empty;
        set
        {
            SetPropertyValue(value);
        }
    }

    public void CancelValidation()
    {
        HasValidationErrors = false;
        _validationDebounce.Cancel();
        PropertyValue = _initialValue ?? string.Empty;
        _boundValue = _initialValue;
        _validationMessage = string.Empty;
        StateHasChanged();
    }

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        set
        {
            if (_hasValidationErrors != value)
            {
                _hasValidationErrors = value;
                OnValidationStateChanged.InvokeAsync(_hasValidationErrors);
            }
        }
    }

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
            _initialValue = null;
            _boundValue = null;
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


    void SetPropertyValue(string value)
    {
        _propertyInfo.SetValue(Model, value);
        StateHasChanged();
    }

    async Task ValidateWithDebounce()
    {
        ValidationResult validationResult = await _validationDebounce
            .ExecuteAsync(() => Validator.ValidateAsync(Model, options => options.IncludeProperties(PropertyName)));

        if (validationResult.IsValid)
        {
            HasValidationErrors = false;
            _validationMessage = string.Empty;
        }
        else
        {
            if (validationResult.Errors.Count != 0)
            {
                _validationMessage = string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage));
                HasValidationErrors = true;
            }
            else
            {
                HasValidationErrors = false;
                _validationMessage = string.Empty;
            }
        }
    }
}