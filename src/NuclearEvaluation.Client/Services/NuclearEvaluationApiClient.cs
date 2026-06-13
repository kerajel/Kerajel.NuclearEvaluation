using System.Net.Http.Json;
using System.Text.Json;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Services;

/// <summary>HTTP-backed implementation of <see cref="INuclearEvaluationApi"/> used by the WASM client.</summary>
public class NuclearEvaluationApiClient : INuclearEvaluationApi
{
    readonly HttpClient _http;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    public NuclearEvaluationApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<DataResult<SeriesView>> GetSeriesViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<SeriesView>("api/views/series", query, ct);

    public Task<DataResult<SampleView>> GetSampleViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<SampleView>("api/views/samples", query, ct);

    public Task<DataResult<SubSampleView>> GetSubSampleViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<SubSampleView>("api/views/subsamples", query, ct);

    public Task<DataResult<ApmView>> GetApmViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<ApmView>("api/views/apm", query, ct);

    public Task<DataResult<ParticleView>> GetParticleViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<ParticleView>("api/views/particles", query, ct);

    public Task<DataResult<ProjectView>> GetProjectViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<ProjectView>("api/views/projects", query, ct);

    public Task<DataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(DataQuery query, CancellationToken ct = default)
        => PostQuery<StemPreviewEntryView>("api/views/stem-entries", query, ct);

    async Task<DataResult<T>> PostQuery<T>(string url, DataQuery query, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(url, query, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                return DataResult<T>.Faulted($"Request failed ({(int)response.StatusCode}).");
            }
            return await response.Content.ReadFromJsonAsync<DataResult<T>>(JsonOptions, ct)
                ?? DataResult<T>.Faulted("Empty response.");
        }
        catch (Exception ex)
        {
            return DataResult<T>.Faulted(ex.Message);
        }
    }

    public async Task<SeriesCountsView> GetSeriesCounts(DataQuery query, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/views/series-counts", query, JsonOptions, ct);
        return await response.Content.ReadFromJsonAsync<SeriesCountsView>(JsonOptions, ct) ?? new SeriesCountsView();
    }

    public async Task<List<int>> GetEnumFilterOptions(string entity, EnumFilterRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync($"api/views/{entity}/enum-options", request, JsonOptions, ct);
        return await response.Content.ReadFromJsonAsync<List<int>>(JsonOptions, ct) ?? [];
    }

    public async Task<List<IsotopeBinCounts>> GetProjectApmUraniumBinCounts(int projectId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<IsotopeBinCounts>>($"api/charts/apm-bin-counts/{projectId}", JsonOptions, ct) ?? [];

    public async Task<List<IsotopeBinCounts>> GetProjectParticleUraniumBinCounts(int projectId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<IsotopeBinCounts>>($"api/charts/particle-bin-counts/{projectId}", JsonOptions, ct) ?? [];

    public async Task<int> CreateSeries(SeriesView seriesView, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/series", seriesView, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(JsonOptions, ct);
    }

    public async Task UpdateSeries(SeriesView seriesView, CancellationToken ct = default)
        => (await _http.PutAsJsonAsync("api/series", seriesView, JsonOptions, ct)).EnsureSuccessStatusCode();

    public async Task DeleteSeries(IReadOnlyCollection<int> seriesIds, CancellationToken ct = default)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, "api/series")
        {
            Content = JsonContent.Create(seriesIds, options: JsonOptions),
        };
        (await _http.SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    public async Task<List<SampleView>> GetSamplesForSeries(int seriesId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<SampleView>>($"api/series/{seriesId}/samples", JsonOptions, ct) ?? [];

    public async Task UpdateProjectField(ProjectFieldUpdate update, CancellationToken ct = default)
        => (await _http.PostAsJsonAsync("api/projects/field", update, JsonOptions, ct)).EnsureSuccessStatusCode();

    public async Task UpdateProjectSeries(ProjectSeriesUpdate update, CancellationToken ct = default)
        => (await _http.PostAsJsonAsync("api/projects/series", update, JsonOptions, ct)).EnsureSuccessStatusCode();

    public async Task<List<PresetFilter>> GetPresetFilters(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PresetFilter>>("api/preset-filters", JsonOptions, ct) ?? [];

    public async Task<int> CreatePresetFilter(PresetFilter filter, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/preset-filters", filter, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(JsonOptions, ct);
    }

    public async Task UpdatePresetFilter(PresetFilter filter, CancellationToken ct = default)
        => (await _http.PutAsJsonAsync("api/preset-filters", filter, JsonOptions, ct)).EnsureSuccessStatusCode();

    public async Task DeletePresetFilter(int id, CancellationToken ct = default)
        => (await _http.DeleteAsync($"api/preset-filters/{id}", ct)).EnsureSuccessStatusCode();

    public async Task<bool> IsProjectNameAvailable(string name, int excludeId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<bool>($"api/projects/name-available?name={Uri.EscapeDataString(name)}&excludeId={excludeId}", JsonOptions, ct);

    public async Task<bool> IsPresetFilterNameAvailable(string name, int excludeId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<bool>($"api/preset-filters/name-available?name={Uri.EscapeDataString(name)}&excludeId={excludeId}", JsonOptions, ct);

    public async Task<OperationOutcome> UploadStemPreviewFile(Guid sessionId, Guid fileId, string fileName, Stream content, CancellationToken ct = default)
    {
        using MultipartFormDataContent form = new();
        form.Add(new StringContent(fileId.ToString()), "fileId");
        StreamContent fileContent = new(content);
        form.Add(fileContent, "file", fileName);

        HttpResponseMessage response = await _http.PostAsync($"api/stem/{sessionId}/files", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            return OperationOutcome.Fail($"Upload failed ({(int)response.StatusCode}).");
        }
        return await response.Content.ReadFromJsonAsync<OperationOutcome>(JsonOptions, ct) ?? OperationOutcome.Fail("Empty response.");
    }

    public async Task DeleteStemPreviewFile(Guid sessionId, Guid fileId, CancellationToken ct = default)
        => (await _http.DeleteAsync($"api/stem/{sessionId}/files/{fileId}", ct)).EnsureSuccessStatusCode();

    public async Task<CaptchaStatus> GetCaptchaStatus(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<CaptchaStatus>("api/captcha/status", JsonOptions, ct) ?? new CaptchaStatus();

    public async Task<CaptchaChallenge> GetCaptchaChallenge(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<CaptchaChallenge>("api/captcha/challenge", JsonOptions, ct) ?? new CaptchaChallenge();

    public async Task<CaptchaStatus> VerifyCaptcha(CaptchaSolution solution, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/captcha/verify", solution, JsonOptions, ct);
        return await response.Content.ReadFromJsonAsync<CaptchaStatus>(JsonOptions, ct) ?? new CaptchaStatus();
    }
}
