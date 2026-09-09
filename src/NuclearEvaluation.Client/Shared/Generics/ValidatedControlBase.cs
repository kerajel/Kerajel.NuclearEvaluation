using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Kerajel.Primitives.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NuclearEvaluation.Shared.Extensions;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Generics;

public class ValidatedTextControlBase<TModel, K> : ComponentBase, IDisposable
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

    /// <summary>
    /// Fires after every validation pass with the current validity, even when the
    /// validity did not flip. Lets a parent re-evaluate dependent UI (e.g. a save
    /// button) for a value that is set in one go and never produces an invalid step.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnValidated { get; set; }

    [Parameter]
    public decimal TooltipOffsetX { get; set; } = 0;

    [Parameter]
    public decimal TooltipOffsetY { get; set; } = 0;

    public IRadzenFormComponent _inputRef = null!;

    protected string? _validationMessage;
    int _validationSequence;

    protected string TooltipOffsetXpx => $"{TooltipOffsetX}px";
    protected string TooltipOffsetYpx => $"{TooltipOffsetY}px";
    protected string ComputedStyle =>
        $"{Style}; border: 2px solid {(!IsValid ? "orange" : "transparent")};";

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
        _validationSequence++;
        _validationDebounce?.Cancel();
        _validationDebounce = new Debouncer<ValidationResult>(DebounceTimeout);

        _propertyInfo = PropertyExpression.GetPropertyInfo();

        _getter = PropertyExpression.Compile();

        _initialValue = PropertyValue;
        _boundValue = _initialValue;

        _validationMessage = string.Empty;
        IsValid = true;
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

        _boundValue = (K?)value;
        SetPropertyValue((K?)value);
        await Validate();
    }

    protected async Task OnValueChanged(K? newValue)
    {
        _boundValue = newValue;
        SetPropertyValue(newValue);
        await Validate();
    }

    public string PropertyName => _propertyInfo.Name;

    public K? PropertyValue
    {
        get => _getter(Model) ?? default;
        set { SetPropertyValue(value); }
    }

    public void CancelValidation()
    {
        _validationSequence++;
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

    public Task CancelChanges()
    {
        SetPropertyValue(_initialValue);
        _boundValue = _initialValue;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _validationSequence++;
        _validationDebounce?.Cancel();
        GC.SuppressFinalize(this);
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
        int sequence = ++_validationSequence;
        IsValid = false;
        await OnValidationStateChanged.InvokeAsync(false);
        try
        {
            return await _validationDebounce.ExecuteAsync(async () =>
            {
                ValidationResult result = null!;
                await InvokeAsync(async () => result = await ValidateCurrent());
                return result;
            });
        }
        catch (OperationCanceledException)
        {
            return new ValidationResult();
        }

        async Task<ValidationResult> ValidateCurrent()
        {
            bool previousIsValid = IsValid;

            ValidationResult validationResult;
            try
            {
                validationResult = await Validator.ValidateAsync(
                    Model,
                    options => options.IncludeProperties(PropertyName)
                );
            }
            catch (HttpRequestException)
            {
                validationResult = new([
                    new ValidationFailure(
                        PropertyName,
                        "Could not validate this value. Please try again."
                    ),
                ]);
            }
            if (sequence != _validationSequence)
                return validationResult;

            if (validationResult.IsValid)
            {
                IsValid = true;
                _validationMessage = string.Empty;
            }
            else
            {
                if (validationResult.Errors.Count > 0)
                {
                    _validationMessage = string.Join(
                        Environment.NewLine,
                        validationResult.Errors.Select(e => e.ErrorMessage)
                    );
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

            await InvokeAsync(() => OnValidated.InvokeAsync(IsValid));

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            return validationResult;
        }
    }
}
