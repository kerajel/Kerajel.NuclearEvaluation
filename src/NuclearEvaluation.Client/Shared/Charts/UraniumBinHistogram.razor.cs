using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;

namespace NuclearEvaluation.Client.Shared.Charts;

public partial class UraniumBinHistogram
{
    [Parameter]
    public int ProjectId { get; set; }

    [Parameter]
    public DataQuery? Query { get; set; }

    [Parameter]
    public int QueryVersion { get; set; }

    [Parameter]
    public bool AwaitingGridQuery { get; set; }

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string? Caption { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<string> Isotopes { get; set; } = [];

    [Parameter, EditorRequired]
    public Func<
        DataQuery,
        CancellationToken,
        Task<List<IsotopeBinCounts>>
    > LoadBinsAsync { get; set; } = null!;

    static readonly ILookup<string, BinCount> EmptyBins = Enumerable
        .Empty<(string Isotope, BinCount Bin)>()
        .ToLookup(value => value.Isotope, value => value.Bin);

    ILookup<string, BinCount> _bins = EmptyBins;
    CancellationTokenSource? _loadCancellation;
    (string Json, int Version)? _lastQuery;
    int? _projectScope;
    bool _isLoading = true;
    bool _hasError;

    bool IsPending => AwaitingGridQuery || _isLoading;

    string PendingLabel =>
        AwaitingGridQuery ? "Waiting for the measurement grid…" : "Loading uranium distributions…";

    protected override async Task OnParametersSetAsync()
    {
        DataQuery query = GetEffectiveQuery();
        int projectScope = query.ProjectId ?? ProjectId;
        if (_projectScope != projectScope)
        {
            _projectScope = projectScope;
            _bins = EmptyBins;
        }

        if (AwaitingGridQuery)
        {
            CancelPendingLoad();
            _lastQuery = null;
            return;
        }

        (string Json, int Version) queryKey = (JsonSerializer.Serialize(query), QueryVersion);
        if (queryKey == _lastQuery)
            return;

        _lastQuery = queryKey;
        await LoadAsync(query);
    }

    public Task Refresh() =>
        AwaitingGridQuery ? Task.CompletedTask : LoadAsync(GetEffectiveQuery());

    DataQuery GetEffectiveQuery() => Query ?? new DataQuery { ProjectId = ProjectId };

    async Task LoadAsync(DataQuery query)
    {
        CancelPendingLoad();
        using CancellationTokenSource cancellation = new();
        _loadCancellation = cancellation;
        _isLoading = true;
        _hasError = false;
        await InvokeAsync(StateHasChanged);

        try
        {
            List<IsotopeBinCounts> data = await LoadBinsAsync(query, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                _bins = data.SelectMany(series =>
                        series.Bins.Select(bin => (series.Isotope, Bin: bin))
                    )
                    .ToLookup(value => value.Isotope, value => value.Bin);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The project query changed or the chart was closed.
        }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (!cancellation.IsCancellationRequested)
                _hasError = true;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    public void Dispose()
    {
        CancelPendingLoad();
        GC.SuppressFinalize(this);
    }

    void CancelPendingLoad()
    {
        CancellationTokenSource? current = _loadCancellation;
        _loadCancellation = null;
        current?.Cancel();
    }
}
