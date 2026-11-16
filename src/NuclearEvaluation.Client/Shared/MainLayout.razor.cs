using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace NuclearEvaluation.Client.Shared;

public partial class MainLayout : IDisposable
{
    bool _desktopNavigation = true;
    bool _phoneNavigation;
    bool _focusNavigation;
    bool _restoreToggleFocus;
    ElementReference _navigationStart;
    ElementReference _phoneToggle;
    ElementReference _mainContent;

    string ShellClass =>
        $"ne-shell{(_desktopNavigation ? " ne-shell-desktop-open" : "")}{(_phoneNavigation ? " ne-shell-phone-open" : "")}";
    string Page =>
        Navigation.ToBaseRelativePath(Navigation.Uri).Split('/', '?', '#')[0].ToLowerInvariant();

    string PageLabel =>
        Page switch
        {
            "fleet" => "Fleet Explorer",
            "fleet-generation" => "Generation Calendar",
            "energy-emissions" => "Energy & Emissions",
            "sources" => "Sources & Methods",
            "data-management" => "Data Management",
            "evaluation" => "Evaluation",
            "projects" => "Evaluation / Project",
            _ => "Nuclear Evaluation",
        };

    string EvaluationNavClass => Page == "projects" ? "ne-nav-link active" : "ne-nav-link";

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_phoneNavigation)
        {
            _ = InvokeAsync(() =>
            {
                ClosePhoneNavigation();
                StateHasChanged();
            });
        }
    }

    void PhoneLayoutChanged(bool isPhone)
    {
        if (!isPhone)
            _phoneNavigation = false;
    }

    void ToggleDesktopNavigation() => _desktopNavigation = !_desktopNavigation;

    void TogglePhoneNavigation()
    {
        if (_phoneNavigation)
        {
            ClosePhoneNavigation();
            return;
        }

        _phoneNavigation = true;
        _focusNavigation = true;
    }

    void ClosePhoneNavigation()
    {
        if (!_phoneNavigation)
            return;

        _phoneNavigation = false;
        _restoreToggleFocus = true;
    }

    void HandleNavigationKeys(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            ClosePhoneNavigation();
    }

    async Task FocusMain()
    {
        try
        {
            await _mainContent.FocusAsync();
        }
        catch (JSException exception)
        {
            Logger.LogDebug(exception, "Unable to focus the main content.");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        bool focusNavigation = _focusNavigation && _phoneNavigation;
        bool restoreToggle = _restoreToggleFocus && !_phoneNavigation;
        _focusNavigation = _restoreToggleFocus = false;

        try
        {
            if (focusNavigation)
                await _navigationStart.FocusAsync(preventScroll: true);
            else if (restoreToggle)
                await _phoneToggle.FocusAsync(preventScroll: true);
        }
        catch (JSException exception)
        {
            Logger.LogDebug(exception, "Unable to move focus for the mobile navigation.");
        }
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
