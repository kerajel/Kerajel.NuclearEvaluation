using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Pages;

public partial class Workbench : IDisposable
{
    readonly CancellationTokenSource _lifetime = new();
    WorkbenchCatalog? _catalog;
    string? _error;
    bool _loading = true;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "view")]
    public string? View { get; set; }

    [SupplyParameterFromQuery(Name = "example")]
    public string? Example { get; set; }

    bool ShowProjects => View == "projects";
    bool ShowBuilder => View == "builder";
    bool ShowImportReview => View == "import";
    bool RunBuilderExample => bool.TryParse(Example, out bool run) && run;

    protected override Task OnInitializedAsync() => LoadCatalogAsync();

    async Task LoadCatalogAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _catalog = await Http.GetFromJsonAsync<WorkbenchCatalog>(
                "api/workbench",
                _lifetime.Token
            );
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            _catalog = null;
            _error = "The workbench could not be reached. Try again shortly.";
        }
        finally
        {
            _loading = false;
        }
    }

    static string Count(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
