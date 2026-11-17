using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NuclearEvaluation.Shared.Contracts;

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
    readonly CancellationTokenSource _lifetime = new();
    SiteFeatures? _features;

    string ShellClass =>
        $"ne-shell{(_desktopNavigation ? " ne-shell-desktop-open" : "")}{(_phoneNavigation ? " ne-shell-phone-open" : "")}";
    string Page =>
        Navigation.ToBaseRelativePath(Navigation.Uri).Split('/', '?', '#')[0].ToLowerInvariant();

    string PageLabel =>
        Page switch
        {
            "radiation" => "Radiation Monitoring",
            "workbench" => "Data Workbench",
            "data-management" when _features?.IsotopeFixturesAvailable == true =>
                "Isotope Lab / Data Management",
            "evaluation" when _features?.IsotopeFixturesAvailable == true =>
                "Isotope Lab / Evaluation",
            "projects" when _features?.IsotopeFixturesAvailable == true => "Isotope Lab / Project",
            "data-management" or "evaluation" or "projects" => "Data Workbench",
            "radiochemistry" => "Radiochemistry",
            "fleet" => "Fleet Explorer",
            "fleet-generation" => "Generation Calendar",
            "energy-emissions" => "Energy & Emissions",
            "sources" => "Sources & Methods",
            _ => "Nuclear Evaluation",
        };

    string EvaluationNavClass => Page == "projects" ? "ne-nav-link active" : "ne-nav-link";

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;

        try
        {
            _features = await Http.GetFromJsonAsync<SiteFeatures>(
                "api/site/features",
                _lifetime.Token
            ) ?? new SiteFeatures(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            Logger.LogDebug(exception, "Unable to load the site features.");
            _features = new SiteFeatures(false);
        }
    }

    void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            ClosePhoneNavigation();
            StateHasChanged();
        });
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
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
