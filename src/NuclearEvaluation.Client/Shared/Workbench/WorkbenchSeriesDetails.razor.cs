using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class WorkbenchSeriesDetails : IDisposable
{
    const int PageSize = 10;
    readonly CancellationTokenSource _lifetime = new();
    CancellationTokenSource? _activeLoad;
    WorkbenchSeries? _scope;
    WorkbenchFilePage? _files;
    RadNetQuery? _observations;
    RadNetQuery? _channels;
    WorkbenchSeriesFile? _file;
    RadNetRow? _observation;
    int _skip;
    bool _loading;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    [Parameter, EditorRequired]
    public WorkbenchSeries Series { get; set; } = null!;

    protected override Task OnParametersSetAsync()
    {
        if (_scope == Series)
            return Task.CompletedTask;

        _scope = Series;
        _skip = 0;
        ResetDrillDown();
        return LoadFilesAsync();
    }

    void ResetDrillDown()
    {
        _file = null;
        _observations = null;
        ResetChannel();
    }

    void ResetChannel()
    {
        _channels = null;
        _observation = null;
    }

    Task PageFilesAsync(int direction)
    {
        _skip += direction * PageSize;
        ResetDrillDown();
        return LoadFilesAsync();
    }

    async Task LoadFilesAsync()
    {
        _activeLoad?.Cancel();
        using CancellationTokenSource load = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token
        );
        _activeLoad = load;
        _loading = true;

        try
        {
            string url = FormattableString.Invariant(
                $"api/workbench/series/{Series.Id:D}/files?skip={_skip}&top={PageSize}"
            );
            WorkbenchFilePage? page = await Http.GetFromJsonAsync<WorkbenchFilePage>(
                url,
                load.Token
            );
            if (ReferenceEquals(_activeLoad, load))
                _files = page;
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ReferenceEquals(_activeLoad, load))
                _files = null;
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, load))
            {
                _activeLoad = null;
                _loading = false;
            }
        }
    }

    void OpenFile(WorkbenchSeriesFile file)
    {
        if (_file?.Id == file.Id)
        {
            ResetDrillDown();
            return;
        }

        _file = file;
        _observations = new(
            RadNetEntity.Observation,
            [Match(RadNetEntity.SourceFile, nameof(RadNetRow.SourceFileId), file.Id), DateScope()],
            SortField: nameof(RadNetRow.ObservedAt),
            Descending: true,
            Top: 15
        );
        ResetChannel();
    }

    void OpenObservation(RadNetRow row)
    {
        if (row.ObservationId is not long id)
            return;

        if (_observation?.ObservationId == id)
        {
            ResetChannel();
            return;
        }

        _observation = row;
        _channels = new(
            RadNetEntity.Channel,
            [Match(RadNetEntity.Observation, nameof(RadNetRow.ObservationId), id)],
            SortField: nameof(RadNetRow.Channel),
            Top: 10
        );
    }

    void OpenQuality(RadNetRow row)
    {
        if (row.SourceFileId is int id)
            Navigation.NavigateTo(SourceUrl(id));
    }

    static string SourceUrl(int fileId) =>
        FormattableString.Invariant($"radiation?view=quality&file={fileId}");

    RadNetFilterGroup DateScope() =>
        new(
            RadNetEntity.Observation,
            false,
            [
                new(nameof(RadNetRow.ObservedAt), RadNetOperator.AtLeast, Day(Series.From)),
                Series.To == DateOnly.MaxValue
                    ? new(
                        nameof(RadNetRow.ObservedAt),
                        RadNetOperator.AtMost,
                        "9999-12-31T23:59:59.9999999"
                    )
                    : new(
                        nameof(RadNetRow.ObservedAt),
                        RadNetOperator.LessThan,
                        Day(Series.To.AddDays(1))
                    ),
            ]
        );

    static RadNetFilterGroup Match(RadNetEntity entity, string field, long value) =>
        new(
            entity,
            false,
            [new(field, RadNetOperator.Equal, value.ToString(CultureInfo.InvariantCulture))]
        );

    static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
