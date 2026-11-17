using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiation.RadNetPresentation;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class WorkbenchProjects : IDisposable
{
    const string ProjectsUrl = "api/workbench/projects";
    const int SeriesPageSize = 8;
    static readonly RadNetQuery ObservationQuery = new(
        RadNetEntity.Observation,
        SortField: nameof(RadNetRow.ObservedAt),
        Descending: true,
        Top: 12
    );
    readonly CancellationTokenSource _lifetime = new();
    IReadOnlyList<WorkbenchProject> _projects = [];
    WorkbenchProjectDetails? _project;
    WorkbenchSeriesPage? _matches;
    Guid? _selectedId;
    string _newProjectName = "";
    string _seriesSearch = "";
    string _appliedSearch = "";
    string? _error;
    string? _notice;
    int _searchSkip;
    int _chartYear;
    CoverageMetric _chartMetric;
    bool _started;
    bool _busy;
    bool _refreshingObservations;
    bool _searchLoading;
    bool _editing;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Parameter]
    public bool Ready { get; set; } = true;

    bool Busy => !Ready || _busy;

    string ChartTitle =>
        _chartMetric switch
        {
            CoverageMetric.Exposure => "Observations with exposure by period",
            CoverageMetric.R03 => "Observations with R03 by period",
            _ => "Observations by period",
        };

    protected override Task OnParametersSetAsync()
    {
        if (!Ready || _started)
            return Task.CompletedTask;

        _started = true;
        return LoadProjectsAsync();
    }

    Task LoadProjectsAsync() =>
        RunAsync(
            async ct =>
            {
                _projects = await Http.GetFromJsonAsync<WorkbenchProject[]>(ProjectsUrl, ct)
                    ?? throw new JsonException("Missing project list.");
                if (_projects.FirstOrDefault() is { } first)
                    await SelectAsync(first.Id, ct);
            },
            refreshObservations: true
        );

    Task OpenAsync(Guid id) => RunAsync(ct => SelectAsync(id, ct), refreshObservations: true);

    async Task SelectAsync(Guid id, CancellationToken ct)
    {
        await LoadDetailsAsync(id, ct);
        _matches = null;
        _editing = false;
        _seriesSearch = "";
        _appliedSearch = "";
        _chartYear = 0;
        _chartMetric = CoverageMetric.Observations;
    }

    async Task LoadDetailsAsync(Guid id, CancellationToken ct)
    {
        WorkbenchProjectDetails detail = await Http.GetFromJsonAsync<WorkbenchProjectDetails>(
            $"{ProjectsUrl}/{id}",
            ct
        ) ?? throw new JsonException("Missing project detail.");
        ShowProject(detail);
    }

    Task CreateAsync() =>
        RunAsync(
            async ct =>
            {
                using HttpResponseMessage response = await Http.PostAsJsonAsync(
                    ProjectsUrl,
                    new WorkbenchProjectDraft(_newProjectName.Trim()),
                    ct
                );
                WorkbenchProjectDetails? created = await ReadChangeAsync(response, ct);
                if (created is null)
                    return;

                _newProjectName = "";
                _editing = false;
                _matches = null;
                _seriesSearch = "";
                _appliedSearch = "";
                _chartYear = 0;
                _chartMetric = CoverageMetric.Observations;
                ShowProject(created);
                _notice = $"{created.Project.Name} created.";
            },
            refreshObservations: true
        );

    Task ChangeMembershipAsync(WorkbenchSeries series, bool add) =>
        RunAsync(
            async ct =>
            {
                if (_project is not { Project: WorkbenchProject project })
                    return;

                string endpoint = FormattableString.Invariant(
                    $"{ProjectsUrl}/{project.Id}/series/{series.Id}?version={project.Version}"
                );
                using HttpRequestMessage request = new(
                    add ? HttpMethod.Post : HttpMethod.Delete,
                    endpoint
                );
                using HttpResponseMessage response = await Http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    await LoadDetailsAsync(project.Id, ct);
                    _notice = "The project changed. Its latest data is loaded. Try again.";
                    return;
                }

                WorkbenchProjectDetails? changed = await ReadChangeAsync(response, ct);
                if (changed is null)
                    return;

                ShowProject(changed);
                _notice = add ? $"{series.Name} added." : $"{series.Name} removed.";
            },
            refreshObservations: true
        );

    async Task<WorkbenchProjectDetails?> ReadChangeAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (
            response.StatusCode
            is HttpStatusCode.BadRequest
                or HttpStatusCode.NotFound
                or HttpStatusCode.Conflict
        )
        {
            string? problem = null;
            if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
            {
                ProjectProblem? body = await response.Content.ReadFromJsonAsync<ProjectProblem>(ct);
                problem = body?.Detail ?? body?.Title;
            }

            _error = problem ?? "The project change could not be saved. Try again.";
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkbenchProjectDetails>(ct)
            ?? throw new JsonException("Missing updated project.");
    }

    void ShowProject(WorkbenchProjectDetails detail)
    {
        _project = detail;
        _selectedId = detail.Project.Id;
        if (_chartYear != 0 && !detail.Stats.Months.Any(month => month.Month.Year == _chartYear))
            _chartYear = 0;

        _projects = _projects
            .Where(project => project.Id != detail.Project.Id)
            .Append(detail.Project)
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    Task ToggleSeriesSearchAsync() =>
        RunAsync(async ct =>
        {
            _editing = !_editing;
            if (_editing)
                await LoadSeriesPageAsync(_appliedSearch, 0, ct);
        });

    Task SearchSeriesAsync() => RunAsync(ct => LoadSeriesPageAsync(_seriesSearch.Trim(), 0, ct));

    Task FindSeriesAsync(int skip) => RunAsync(ct => LoadSeriesPageAsync(_appliedSearch, skip, ct));

    async Task LoadSeriesPageAsync(string search, int skip, CancellationToken ct)
    {
        string endpoint = FormattableString.Invariant(
            $"api/workbench/series?search={Uri.EscapeDataString(search)}&sort=Name&skip={skip}&top={SeriesPageSize}"
        );
        _searchLoading = true;
        try
        {
            _matches = await Http.GetFromJsonAsync<WorkbenchSeriesPage>(endpoint, ct)
                ?? throw new JsonException("Missing Series results.");
            _appliedSearch = search;
            _searchSkip = skip;
        }
        finally
        {
            _searchLoading = false;
        }
    }

    Task RefreshProjectAsync() =>
        RunAsync(
            async ct =>
            {
                if (_selectedId is not Guid id)
                    return;
                await LoadDetailsAsync(id, ct);
                _notice = "The project changed. Its latest details are loaded.";
            },
            refreshObservations: true
        );

    static string ObservationUrl(WorkbenchProject project) =>
        FormattableString.Invariant(
            $"{ProjectsUrl}/{project.Id}/observations/query?version={project.Version}"
        );

    static string? ObservationSourceLink(RadNetRow row) =>
        row.SourceFileId is int sourceId
            ? FormattableString.Invariant($"radiation?view=quality&file={sourceId}")
            : null;

    async Task RunAsync(Func<CancellationToken, Task> action, bool refreshObservations = false)
    {
        if (Busy || _lifetime.IsCancellationRequested)
            return;

        _busy = true;
        _refreshingObservations = refreshObservations;
        _error = _notice = null;
        CancellationToken ct = _lifetime.Token;
        try
        {
            await action(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            _error = "The project request could not be completed. Try again.";
        }
        finally
        {
            _busy = false;
            _refreshingObservations = false;
        }
    }

    static IEnumerable<int> Years(IReadOnlyList<WorkbenchMonth> months) =>
        months.Select(month => month.Month.Year).Distinct().OrderDescending();

    ChartBucket[] ChartBuckets(IReadOnlyList<WorkbenchMonth> months)
    {
        if (_chartYear != 0)
        {
            Dictionary<int, long> countsByMonth = months
                .Where(month => month.Month.Year == _chartYear)
                .ToDictionary(month => month.Month.Month, ValueFor);
            return Enumerable
                .Range(1, 12)
                .Select(month => new ChartBucket(
                    CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month),
                    countsByMonth.GetValueOrDefault(month)
                ))
                .ToArray();
        }

        return months
            .GroupBy(month => month.Month.Year)
            .OrderBy(group => group.Key)
            .Select(group => new ChartBucket(
                group.Key.ToString(CultureInfo.InvariantCulture),
                group.Sum(ValueFor)
            ))
            .ToArray();
    }

    long ValueFor(WorkbenchMonth month) =>
        _chartMetric switch
        {
            CoverageMetric.Exposure => month.WithExposure,
            CoverageMetric.R03 => month.WithR03,
            _ => month.ObservationCount,
        };

    static string BarSize(long count, long scale) =>
        FormattableString.Invariant($"{100d * count / scale:0.##}%");

    static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string PageRange(int skip, int length, long total) =>
        $"{Count(skip + 1)}–{Count(skip + length)} of {Count(total)}";

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    enum CoverageMetric
    {
        Observations,
        Exposure,
        R03,
    }

    readonly record struct ChartBucket(string Label, long Count);

    sealed record ProjectProblem(string? Title, string? Detail);
}
