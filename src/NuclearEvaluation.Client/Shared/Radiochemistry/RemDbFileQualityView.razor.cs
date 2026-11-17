using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiochemistry;

public partial class RemDbFileQualityView : IDisposable
{
    const int PageSize = 25;
    readonly CancellationTokenSource _lifetime = new();
    RemDbFilePage? _page;
    RemDbFileQuality? _quality;
    string? _error;
    int _skip;
    int _requestedSkip;
    bool _loading = true;

    [Parameter]
    public int? FileId { get; set; }

    [Parameter]
    public EventCallback<RemDbSourceFile> OnBrowse { get; set; }

    [Inject]
    HttpClient Http { get; set; } = null!;

    protected override Task OnInitializedAsync() => LoadAsync(0);

    Task RetryAsync() => LoadAsync(_requestedSkip);

    async Task LoadAsync(int skip)
    {
        _loading = true;
        _requestedSkip = skip;
        _error = null;
        try
        {
            if (FileId is int file)
            {
                _quality = await Http.GetFromJsonAsync<RemDbFileQuality>(
                    FormattableString.Invariant($"api/remdb/files/{file}/quality"),
                    _lifetime.Token
                );
            }
            else
            {
                _page = await Http.GetFromJsonAsync<RemDbFilePage>(
                    FormattableString.Invariant($"api/remdb/files?skip={skip}&top={PageSize}"),
                    _lifetime.Token
                ) ?? throw new JsonException("Missing source files response.");
                _skip = skip;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            _error = error is HttpRequestException { StatusCode: HttpStatusCode.NotFound }
                ? "The source file does not exist in this snapshot."
                : "Source file coverage could not be loaded.";
        }
        finally
        {
            _loading = false;
        }
    }

    Task TurnPageAsync(int direction) =>
        _loading ? Task.CompletedTask : LoadAsync(_skip + direction * PageSize);

    static (string Label, IReadOnlyList<RemDbCoverageValue> Values)[] Breakdown(
        RemDbFileQuality report
    ) => [("Reported units", report.Units), ("Reported value types", report.ResultTypes)];

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
