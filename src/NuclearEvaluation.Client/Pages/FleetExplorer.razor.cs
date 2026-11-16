using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Shared.Fleet;

namespace NuclearEvaluation.Client.Pages;

public partial class FleetExplorer : IDisposable
{
    static readonly decimal[] CapacityLegendFractions = [0.25m, 0.5m, 1m];

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    ILogger<FleetExplorer> Logger { get; set; } = null!;

    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    [Inject]
    IJSRuntime Js { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "plant")]
    string? InitialPlantId { get; set; }

    [SupplyParameterFromQuery(Name = "compare")]
    string? InitialComparisonId { get; set; }

    readonly CancellationTokenSource _lifetime = new();
    FleetSnapshot? _snapshot;
    FleetPlant? _selected;
    int? _comparisonId;
    (string? Plant, string? Comparison) _appliedSelection;
    bool _loading;
    bool _showCapacity;
    bool _choosingComparison;
    decimal _capacityScaleMw = 5_000;
    string _search = "";
    string _stateFilter = "";
    FleetState[] _states = [];
    StateMetric _stateMetric;
    MonthGeneration[] _primarySeries = [];
    MonthGeneration[] _comparisonSeries = [];
    ShareFeedback _shareFeedback;
    ElementReference _manualShareLink;
    bool _focusManualShareLink;

    FleetPlant? Comparison => _snapshot?.Plants.FirstOrDefault(plant => plant.Id == _comparisonId);

    protected override Task OnInitializedAsync() => LoadSnapshot();

    protected override void OnParametersSet()
    {
        if (_snapshot is not null && (InitialPlantId, InitialComparisonId) != _appliedSelection)
            SelectInitialPlant();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusManualShareLink || _shareFeedback != ShareFeedback.ManualLink)
            return;

        _focusManualShareLink = false;
        await _manualShareLink.FocusAsync();
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
            _states = FleetState.Summarize(_snapshot.Plants);
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
        int? primaryId = ReadPlantId(InitialPlantId);
        int? comparisonId = ReadPlantId(InitialComparisonId);
        FleetPlant selected = plants.FirstOrDefault(plant => plant.Id == primaryId)
            ?? plants.MaxBy(plant => plant.NetSummerCapacityMw)!;
        _appliedSelection = (InitialPlantId, InitialComparisonId);
        _selected = selected;
        _comparisonId =
            comparisonId != selected.Id && plants.Any(plant => plant.Id == comparisonId)
                ? comparisonId
                : null;
        _choosingComparison = false;
        UpdateMonthlySeries();
    }

    static int? ReadPlantId(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0
            ? id
            : null;

    FleetPlant[] FilterPlants()
    {
        string text = _search.Trim();
        if (text.Length == 0 && _stateFilter.Length == 0)
            return _snapshot!.Plants;

        return _snapshot!
            .Plants.Where(plant =>
                (_stateFilter.Length == 0 || plant.State == _stateFilter)
                && (
                    text.Length == 0
                    || plant.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || plant.State.Equals(text, StringComparison.OrdinalIgnoreCase)
                    || FleetState
                        .DisplayName(plant.State)
                        .Contains(text, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToArray();
    }

    FleetState[] RankStates() => _states.OrderByDescending(StateValue).ToArray();

    decimal? StateValue(FleetState state) =>
        _stateMetric == StateMetric.Capacity
            ? state.NetSummerCapacityMw / 1_000
            : state.AnnualNetGenerationMwh / 1_000_000;

    void ToggleState(string state) => _stateFilter = _stateFilter == state ? "" : state;

    static string StateBarStyle(decimal? value, decimal? maximum)
    {
        decimal fraction = maximum is > 0 ? Math.Clamp((value ?? 0) / maximum.Value, 0, 1) : 0;
        return FormattableString.Invariant($"--fleet-state-bar:{fraction * 100:F2}%");
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
        _shareFeedback = ShareFeedback.None;
        _focusManualShareLink = false;
        _primarySeries = GetMonthlySeries(_selected);
        _comparisonSeries = GetMonthlySeries(Comparison);
    }

    string ComparisonLink(int firstPlant, int secondPlant) =>
        Navigation
            .ToAbsoluteUri(
                FormattableString.Invariant($"fleet?plant={firstPlant}&compare={secondPlant}")
            )
            .AbsoluteUri;

    async Task CopyComparisonLink(int firstPlant, int secondPlant)
    {
        string link = ComparisonLink(firstPlant, secondPlant);
        ShareFeedback feedback;
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", _lifetime.Token, link);
            feedback = ShareFeedback.Copied;
        }
        catch (JSException)
        {
            feedback = ShareFeedback.ManualLink;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }

        if (_selected?.Id == firstPlant && _comparisonId == secondPlant)
        {
            _shareFeedback = feedback;
            _focusManualShareLink = feedback == ShareFeedback.ManualLink;
        }
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

    static string EmissionsEstimateUrl(decimal generationMwh) =>
        FormattableString.Invariant(
            $"energy-emissions?generationGwh={generationMwh / 1_000}#emissions-estimate"
        );

    static string FormatGeneration(FleetPlant plant, int month) =>
        plant.MonthlyNetGenerationMwh.ElementAtOrDefault(month)?.ToString("N1") ?? "—";

    static string FormatMonth(int month) =>
        DateTimeFormatInfo.InvariantInfo.GetAbbreviatedMonthName(month);

    static string FormatChartMonth(object value) =>
        value switch
        {
            DateTime date => FormatMonth(date.Month),
            DateOnly date => FormatMonth(date.Month),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
        };

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    sealed record MonthGeneration(DateTime Month, decimal GenerationGwh);

    enum StateMetric
    {
        Capacity,
        Generation,
    }

    enum ShareFeedback
    {
        None,
        Copied,
        ManualLink,
    }
}
