using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Shared.Fleet;

namespace NuclearEvaluation.Client.Pages;

public partial class FleetExplorer : IDisposable
{
    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    ILogger<FleetExplorer> Logger { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "plant")]
    int? InitialPlantId { get; set; }

    readonly CancellationTokenSource _lifetime = new();
    FleetSnapshot? _snapshot;
    FleetPlant? _selected;
    int? _comparisonId;
    int? _appliedPlantId;
    bool _loading;
    bool _showCapacity;
    bool _choosingComparison;
    decimal _capacityScaleMw = 5_000;
    string _search = "";
    MonthGeneration[] _primarySeries = [];
    MonthGeneration[] _comparisonSeries = [];

    FleetPlant? Comparison => _snapshot?.Plants.FirstOrDefault(plant => plant.Id == _comparisonId);

    protected override Task OnInitializedAsync() => LoadSnapshot();

    protected override void OnParametersSet()
    {
        if (_snapshot is not null && InitialPlantId != _appliedPlantId)
            SelectInitialPlant();
    }

    async Task LoadSnapshot()
    {
        _loading = true;
        try
        {
            FleetSnapshot? data = await Http.GetFromJsonAsync<FleetSnapshot>(
                FleetSnapshot.AssetPath,
                _lifetime.Token
            );
            if (data is null || data.Plants.Length == 0)
                return;

            _snapshot = data with
            {
                Plants = data
                    .Plants.OrderBy(plant => plant.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            };
            _capacityScaleMw = Math.Max(
                1,
                _snapshot.Plants.Max(plant => plant.NetSummerCapacityMw) ?? 0
            );
            SelectInitialPlant();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            Logger.LogWarning(exception, "Unable to load the Fleet Explorer data snapshot.");
        }
        finally
        {
            _loading = false;
        }
    }

    void SelectInitialPlant()
    {
        FleetPlant[] plants = _snapshot!.Plants;
        FleetPlant selected = plants.FirstOrDefault(plant => plant.Id == InitialPlantId)
            ?? plants.MaxBy(plant => plant.NetSummerCapacityMw)!;
        _appliedPlantId = InitialPlantId;
        _selected = selected;
        if (_comparisonId == selected.Id)
            _comparisonId = null;
        _choosingComparison = false;
        UpdateMonthlySeries();
    }

    FleetPlant[] FilterPlants()
    {
        string text = _search.Trim();
        if (text.Length == 0)
            return _snapshot!.Plants;

        return _snapshot!
            .Plants.Where(plant =>
                plant.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || plant.State.Equals(text, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
    }

    void SelectPlant(FleetPlant plant)
    {
        if (_choosingComparison)
        {
            if (plant.Id == _selected?.Id)
                return;

            _comparisonId = plant.Id;
        }
        else
        {
            if (plant.Id == _comparisonId)
                _comparisonId = _selected?.Id;

            _selected = plant;
            _choosingComparison = true;
        }
        UpdateMonthlySeries();
    }

    void ClearComparison()
    {
        _comparisonId = null;
        _choosingComparison = true;
        UpdateMonthlySeries();
    }

    void UpdateMonthlySeries()
    {
        _primarySeries = GetMonthlySeries(_selected);
        _comparisonSeries = GetMonthlySeries(Comparison);
    }

    MonthGeneration[] GetMonthlySeries(FleetPlant? plant) =>
        plant is null
            ? []
            : plant
                .MonthlyNetGenerationMwh.Select((value, month) => new { value, month })
                .Where(item => item.value.HasValue)
                .Select(item => new MonthGeneration(
                    new DateTime(_snapshot!.Year, item.month + 1, 1),
                    item.value!.Value / 1_000
                ))
                .ToArray();

    static string FormatMetric(decimal? value, string units) =>
        value.HasValue ? $"{value:N1} {units}" : "—";

    static string FormatGeneration(FleetPlant plant, int month) =>
        plant.MonthlyNetGenerationMwh.ElementAtOrDefault(month)?.ToString("N1") ?? "—";

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    sealed record MonthGeneration(DateTime Month, decimal GenerationGwh);
}
