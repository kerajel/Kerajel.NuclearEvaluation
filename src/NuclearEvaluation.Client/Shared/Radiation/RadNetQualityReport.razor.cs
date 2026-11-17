using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiation;

public partial class RadNetQualityReport : IDisposable
{
    readonly CancellationTokenSource _lifetime = new();
    RadNetFileQuality? _quality;
    bool _loading = true;
    string? _error;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Parameter, EditorRequired]
    public int FileId { get; set; }

    [Parameter]
    public EventCallback<RadNetRow> OnBrowse { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _quality = await Http.GetFromJsonAsync<RadNetFileQuality>(
                FormattableString.Invariant($"api/radnet/files/{FileId}/quality"),
                _lifetime.Token
            );
            if (_quality is null)
                _error = "File coverage is unavailable.";
        }
        catch (Exception error) when (error is HttpRequestException or JsonException)
        {
            _error = "File coverage is unavailable.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        finally
        {
            _loading = false;
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
