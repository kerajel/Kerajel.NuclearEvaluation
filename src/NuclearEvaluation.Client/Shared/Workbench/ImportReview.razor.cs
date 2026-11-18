using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class ImportReview : IDisposable
{
    const string ReviewUrl = "api/workbench/import-review";
    const string LargeCsvUrl = $"{ReviewUrl}/large";
    const string LargeCsvQueryUrl = $"{LargeCsvUrl}/query";
    static readonly RadNetQuery FullArchiveQuery = new(RadNetEntity.Observation);
    readonly CancellationTokenSource _lifetime = new();
    IReadOnlyList<WorkbenchProject> _projects = [];
    ImportReviewAvailability? _availability;
    ImportReviewStage? _stage;
    ImportReviewLargeStage? _largeStage;
    ImportReviewSeriesLink? _seriesLink;
    IBrowserFile? _upload;
    string _projectChoice = "";
    string? _error;
    string? _progress;
    bool _busy;
    bool _requestedAvailability;
    int _largeStageRevision;
    ReviewView _view;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    [Parameter]
    public long ObservationCount { get; set; }

    [Parameter]
    public bool Ready { get; set; } = true;

    static long UploadMegabytes => ImportReviewCatalog.MaxUploadBytes / 1_048_576;

    static string FileSize(long bytes) =>
        FormattableString.Invariant($"{bytes / 1_048_576d:N1} MiB");

    protected override Task OnParametersSetAsync()
    {
        if (!Ready || _requestedAvailability)
            return Task.CompletedTask;

        _requestedAvailability = true;
        return LoadAvailabilityAsync();
    }

    Task LoadAvailabilityAsync() => RunAsync(null, async ct =>
        {
            using HttpResponseMessage response = await Http.GetAsync(ReviewUrl, ct);
            _availability = await ReadAsync<ImportReviewAvailability>(response, ct);
        });

    Task StageSampleAsync() => RunAsync("Staging the EPA sample…", async ct =>
        {
            using HttpResponseMessage response = await Http.PostAsync(
                $"{ReviewUrl}/sample",
                null,
                ct
            );
            await ShowStageAsync(await ReadAsync<ImportReviewStage>(response, ct), ct);
        });

    Task StageCsvCollectionAsync() =>
        RunAsync("Preparing the CSV collection in DuckDB…", async ct =>
        {
            using HttpResponseMessage response = await Http.PostAsync(
                $"{LargeCsvUrl}/stage",
                null,
                ct
            );
            _largeStage = await ReadAsync<ImportReviewLargeStage>(response, ct);
            _largeStageRevision++;
        });

    void SelectUpload(InputFileChangeEventArgs change)
    {
        IBrowserFile file = change.File;
        _error = null;
        _upload = null;
        if (!file.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            _error = "Choose a .csv file.";
            return;
        }
        if (file.Size is <= 0 or > ImportReviewCatalog.MaxUploadBytes)
        {
            _error =
                $"Choose a nonempty CSV up to {UploadMegabytes.ToString(CultureInfo.InvariantCulture)} MB.";
            return;
        }
        _upload = file;
    }

    Task UploadAsync() =>
        _upload is { } file ? RunAsync("Uploading and staging the CSV…", async ct =>
            {
                using MultipartFormDataContent form = new();
                using StreamContent csv = new(
                    file.OpenReadStream(ImportReviewCatalog.MaxUploadBytes, ct)
                );
                csv.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                form.Add(csv, "file", file.Name);
                using HttpResponseMessage response = await Http.PostAsync(
                    $"{ReviewUrl}/upload",
                    form,
                    ct
                );
                await ShowStageAsync(await ReadAsync<ImportReviewStage>(response, ct), ct);
            }) : Task.CompletedTask;

    async Task ShowStageAsync(ImportReviewStage stage, CancellationToken ct)
    {
        _stage = stage;
        _seriesLink = null;
        if (stage.CanCreateSeries)
        {
            using HttpResponseMessage response = await Http.GetAsync("api/workbench/projects", ct);
            _projects = await ReadAsync<WorkbenchProject[]>(response, ct) ?? [];
            if (!_projects.Any(project => project.Id.ToString("D") == _projectChoice))
                _projectChoice = "";
        }
    }

    Task CreateSeriesAsync() =>
        _stage is { CanCreateSeries: true } stage ? RunAsync("Saving the Series…", async ct =>
            {
                ImportReviewCreateSeries request = new(
                    Guid.TryParse(_projectChoice, out Guid id) ? id : null
                );
                using HttpResponseMessage response = await Http.PostAsJsonAsync(
                    $"{ReviewUrl}/{stage.Id:D}/series",
                    request,
                    ct
                );
                _seriesLink = await ReadAsync<ImportReviewSeriesLink>(response, ct);
            }) : Task.CompletedTask;

    async Task RunAsync(string? progress, Func<CancellationToken, Task> action)
    {
        if (!Ready || _busy)
            return;
        _busy = true;
        _progress = progress;
        _error = null;
        try
        {
            await action(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ImportRequestException error)
        {
            _error = error.Message;
        }
        catch (Exception error)
            when (error
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or OperationCanceledException
            )
        {
            _error = "The import review request could not be completed. Try again.";
        }
        finally
        {
            _progress = null;
            _busy = false;
        }
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            ImportProblem? problem =
                response.Content.Headers.ContentType?.MediaType == "application/problem+json"
                    ? await response.Content.ReadFromJsonAsync<ImportProblem>(ct)
                    : null;
            throw new ImportRequestException(
                problem?.Detail
                    ?? problem?.Title
                    ?? "The import review request could not be completed."
            );
        }
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new JsonException("Missing import review response.");
    }

    string? ProjectName(Guid id) => _projects.FirstOrDefault(project => project.Id == id)?.Name;

    void OpenFileQuality(RadNetRow row)
    {
        if (row.SourceFileId is { } fileId)
            Navigation.NavigateTo(QualityUrl(fileId));
    }

    static string QualityUrl(int fileId) =>
        FormattableString.Invariant($"radiation?view=quality&file={fileId}");

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed record ImportProblem(string? Title, string? Detail);

    sealed class ImportRequestException(string message) : Exception(message);

    enum ReviewView
    {
        AnnualCsv,
        CsvCollection,
        FullArchive,
    }
}
