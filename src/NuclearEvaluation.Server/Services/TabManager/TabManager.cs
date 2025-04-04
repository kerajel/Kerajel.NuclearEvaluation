using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.JSInterop;

namespace NuclearEvaluation.Server.Services.TabManager;

public class TabManager
{
    private readonly Dictionary<string, int> _tabIndexes = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<int, string> _indexTabs = [];
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly string _defaultTabName;
    private readonly string _tabQueryParameterName;

    public int SelectedTabIndex { get; set; } = 0;

    public Uri Uri { get; private set; } = null!;

    public TabManager(NavigationManager navigationManager, IJSRuntime jsRuntime, string defaultTabName, string tabQueryParameterName = "tab")
    {
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
        _defaultTabName = defaultTabName;
        _tabQueryParameterName = tabQueryParameterName;
        Uri = new(navigationManager.Uri);
    }

    public TabManager AddTab(string name, int index)
    {
        _tabIndexes[name] = index;
        _indexTabs[index] = name;
        return this;
    }

    public TabManager Initialize()
    {
        SetInitialTabFromUri();
        return this;
    }

    private void SetInitialTabFromUri()
    {
        Uri uri = new(_navigationManager.Uri);
        Dictionary<string, StringValues> queryParams = QueryHelpers.ParseQuery(uri.Query);
        if (queryParams.TryGetValue(_tabQueryParameterName, out StringValues tabValue))
        {
            string tabQuery = tabValue.FirstOrDefault() ?? string.Empty;
            if (_tabIndexes.TryGetValue(tabQuery, out int tabIndex))
            {
                SelectedTabIndex = tabIndex;
            }
        }
    }

    public async Task OnTabChanged(int index)
    {
        SelectedTabIndex = index;
        string tabName = _indexTabs.TryGetValue(index, out string? value) ? value : _defaultTabName;
        string newUri = QueryHelpers.AddQueryString(
            _navigationManager.ToAbsoluteUri(_navigationManager.Uri).GetLeftPart(UriPartial.Path),
            _tabQueryParameterName,
            tabName);

        Uri = new(newUri);

        await _jsRuntime.InvokeVoidAsync("updateUrlWithoutReloading", newUri);
    }
}