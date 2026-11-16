using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Fleet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Pages;

public partial class FleetGeneration : IAsyncDisposable
{
    const int MonthCount = 12;
    const int HeatLevels = 5;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    IJSRuntime Js { get; set; } = null!;

    [Inject]
    ILogger<FleetGeneration> Logger { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "state")]
    public string? InitialState { get; set; }

    readonly CancellationTokenSource _lifetime = new();
    StateMonths[] _states = [];
    int[] _daysInMonth = [];
    int _year;
    int _stateIndex;
    int _monthIndex;
    bool _loading;
    bool _averagePerDay;
    decimal _maximumTotal;
    decimal _maximumAverage;
    string? _appliedState;
    ElementReference _viewport;
    IJSObjectReference? _keyboard;
    bool _keyboardAttempted;
    bool _revealRequested;
    long _manualSelectionRevision;
    string? _totalCsvUrl;
    string? _dailyCsvUrl;

    string Units => _averagePerDay ? "GWh/day" : "GWh";
    decimal ScaleMaximum => _averagePerDay ? _maximumAverage : _maximumTotal;
    string CsvDownloadUrl =>
        _averagePerDay
            ? _dailyCsvUrl ??= BuildCsvUrl(averagePerDay: true)
            : _totalCsvUrl ??= BuildCsvUrl(averagePerDay: false);
    string CsvFileName =>
        FormattableString.Invariant(
            $"us-nuclear-generation-{_year:D4}-{(_averagePerDay ? "daily-average" : "monthly")}.csv"
        );

    protected override Task OnInitializedAsync() => LoadSnapshotAsync();

    protected override void OnParametersSet()
    {
        if (_states.Length > 0 && InitialState != _appliedState)
            ApplyInitialState();
    }

    async Task LoadSnapshotAsync()
    {
        _loading = true;
        try
        {
            FleetSnapshot? snapshot = await Http.GetFromJsonAsync<FleetSnapshot>(
                FleetSnapshot.AssetPath,
                _lifetime.Token
            );
            if (snapshot is not { Year: >= 1 and <= 9999, Plants: { Length: > 0 } plants })
                return;

            _year = snapshot.Year;
            _daysInMonth = Enumerable
                .Range(1, MonthCount)
                .Select(month => DateTime.DaysInMonth(_year, month))
                .ToArray();
            _states = plants
                .GroupBy(plant => plant.State, StringComparer.Ordinal)
                .Select(CreateStateMonths)
                .OrderBy(state => state.Name, StringComparer.Ordinal)
                .ToArray();
            _maximumTotal = MaximumGeneration(averagePerDay: false);
            _maximumAverage = MaximumGeneration(averagePerDay: true);
            _totalCsvUrl = _dailyCsvUrl = null;
            ApplyInitialState();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            Logger.LogWarning(exception, "Unable to load the Generation Calendar snapshot.");
        }
        finally
        {
            _loading = false;
        }
    }

    static StateMonths CreateStateMonths(IGrouping<string, FleetPlant> state)
    {
        FleetPlant[] plants = state
            .OrderBy(plant => plant.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        decimal?[] generation = Enumerable
            .Range(0, MonthCount)
            .Select(month =>
                FleetSnapshot.SumComplete(plants.Select(plant => PlantGeneration(plant, month)))
            )
            .ToArray();
        return new(FleetState.DisplayName(state.Key), state.Key, plants, generation);
    }

    decimal MaximumGeneration(bool averagePerDay) =>
        _states
            .SelectMany(state =>
                state.GenerationMwh.Select((mwh, month) => GenerationGwh(mwh, month, averagePerDay))
            )
            .Max(gwh => Math.Max(0, gwh ?? 0));

    void ApplyInitialState()
    {
        _appliedState = InitialState;
        _stateIndex = Math.Max(
            0,
            Array.FindIndex(_states, state =>
                state.Code.Equals(InitialState, StringComparison.OrdinalIgnoreCase)
            )
        );
        _revealRequested = true;
        _manualSelectionRevision++;
        PublishAssistantContext();
    }

    void SelectState(int state) => SelectCell(state, _monthIndex);

    void SelectMonth(int month) => SelectCell(_stateIndex, month);

    void SelectCell(int state, int month) => ChangeView(state, month, _averagePerDay);

    void SelectDailyAverage(bool averagePerDay) =>
        ChangeView(_stateIndex, _monthIndex, averagePerDay);

    void ChangeView(int state, int month, bool averagePerDay)
    {
        if (state == _stateIndex && month == _monthIndex && averagePerDay == _averagePerDay)
            return;

        SetView(state, month, averagePerDay);
        _manualSelectionRevision++;
        PublishAssistantContext();
    }

    void SetView(int state, int month, bool averagePerDay)
    {
        _stateIndex = state;
        _monthIndex = month;
        _averagePerDay = averagePerDay;
        _revealRequested = true;
    }

    void PublishAssistantContext()
    {
        if (_lifetime.IsCancellationRequested)
            return;

        long revision = _manualSelectionRevision;
        AssistantWorkspace.Set(
            this,
            new(AssistantPage.FleetGeneration, AssistantSurface.GenerationCalendar),
            action => ApplyAssistantActionAsync(action, revision),
            revision
        );
    }

    async Task<bool> ApplyAssistantActionAsync(AssistantAction action, long revision)
    {
        if (
            _lifetime.IsCancellationRequested
            || revision != _manualSelectionRevision
            || action.Kind != AssistantActionKind.SelectGenerationView
            || action.GenerationSelection is not { } selection
            || selection is { StateCode: null, Month: null, DailyAverage: null }
            || selection.Month is < 1 or > MonthCount
        )
            return false;

        int stateIndex = selection.StateCode is null
            ? _stateIndex
            : Array.FindIndex(_states, state =>
                state.Code.Equals(selection.StateCode, StringComparison.OrdinalIgnoreCase)
            );
        if (stateIndex < 0)
            return false;

        int monthIndex = selection.Month is int month ? month - 1 : _monthIndex;
        SetView(stateIndex, monthIndex, selection.DailyAverage ?? _averagePerDay);
        await InvokeAsync(StateHasChanged);
        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_states.Length == 0)
            return;

        if (!_keyboardAttempted)
        {
            _keyboardAttempted = true;
            try
            {
                _keyboard = await Js.InvokeAsync<IJSObjectReference>(
                    "import",
                    _lifetime.Token,
                    "./scripts/fleetGeneration.js"
                );
                if (_keyboard is not null)
                    await _keyboard.InvokeVoidAsync("listen", _lifetime.Token, _viewport);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (JSException exception)
            {
                Logger.LogWarning(
                    exception,
                    "Unable to attach Generation Calendar keyboard navigation."
                );
            }
        }

        if (_revealRequested && _keyboard is not null && !_lifetime.IsCancellationRequested)
        {
            _revealRequested = false;
            try
            {
                await _keyboard.InvokeVoidAsync(
                    "reveal",
                    _lifetime.Token,
                    _viewport,
                    _stateIndex,
                    _monthIndex
                );
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (JSException exception)
            {
                Logger.LogDebug(
                    exception,
                    "Unable to scroll to the selected month in the Generation Calendar."
                );
            }
        }
    }

    static decimal? PlantGeneration(FleetPlant plant, int month) =>
        plant.MonthlyNetGenerationMwh.ElementAtOrDefault(month);

    decimal? DisplayedGeneration(decimal? mwh, int month) =>
        GenerationGwh(mwh, month, _averagePerDay);

    decimal? GenerationGwh(decimal? mwh, int month, bool averagePerDay) =>
        mwh / (averagePerDay ? 1_000 * _daysInMonth[month] : 1_000);

    string BuildCsvUrl(bool averagePerDay)
    {
        StringBuilder csv = new("state_code,state,month,");
        csv.Append(
                averagePerDay ? "average_daily_net_generation_gwh_per_day" : "net_generation_gwh"
            )
            .Append("\r\n");
        foreach (StateMonths state in _states)
        {
            for (int month = 0; month < MonthCount; month++)
            {
                csv.Append(state.Code)
                    .Append(',')
                    .Append(state.Name)
                    .Append(',')
                    .Append(CultureInfo.InvariantCulture, $"{_year:D4}-{month + 1:D2},");
                if (
                    GenerationGwh(state.GenerationMwh[month], month, averagePerDay) is decimal value
                )
                {
                    decimal rounded = averagePerDay ? decimal.Round(value, 6) : value;
                    csv.Append(
                        (rounded == 0 ? value : rounded).ToString(
                            "G29",
                            CultureInfo.InvariantCulture
                        )
                    );
                }
                csv.Append("\r\n");
            }
        }

        return "data:text/csv;charset=utf-8;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    string HeatClass(decimal? value)
    {
        if (value is null)
            return "generation-missing";
        if (value < 0)
            return "generation-negative";
        if (value == 0)
            return "generation-zero";

        int level = Math.Clamp(
            (int)Math.Ceiling(value.Value / ScaleMaximum * HeatLevels),
            1,
            HeatLevels
        );
        return $"generation-level-{level}";
    }

    static string FormatCell(decimal? value) =>
        value switch
        {
            null => "—",
            0 => "0",
            decimal amount when Math.Abs(amount) < 0.01m => amount.ToString("0.#E+0"),
            decimal amount when Math.Abs(amount) < 10 => amount.ToString("0.##"),
            decimal amount => amount.ToString("N0"),
        };

    static string FormatExact(decimal? value) =>
        value switch
        {
            null => "—",
            decimal amount when amount != 0 && Math.Abs(amount) < 0.001m => amount.ToString(
                "0.###E+0"
            ),
            decimal amount => amount.ToString("#,##0.###"),
        };

    static string MonthName(int month, bool abbreviated = false) =>
        abbreviated
            ? DateTimeFormatInfo.InvariantInfo.GetAbbreviatedMonthName(month + 1)
            : DateTimeFormatInfo.InvariantInfo.GetMonthName(month + 1);

    string CellLabel(string state, int month, decimal? value) =>
        value is null
            ? $"{state}, {MonthName(month)} {_year}. Missing reported generation."
            : $"{state}, {MonthName(month)} {_year}. {FormatExact(value)} {Units}.";

    static string EmissionsEstimateUrl(decimal mwh) =>
        FormattableString.Invariant(
            $"energy-emissions?generationGwh={mwh / 1_000}#emissions-estimate"
        );

    public async ValueTask DisposeAsync()
    {
        AssistantWorkspace.Remove(this);
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
        if (_keyboard is not null)
            await _keyboard.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    sealed record StateMonths(
        string Name,
        string Code,
        FleetPlant[] Plants,
        decimal?[] GenerationMwh
    );
}
