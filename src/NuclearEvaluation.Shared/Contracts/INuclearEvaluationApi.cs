using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Shared.Contracts;

/// <summary>
/// The full data surface the WASM client consumes. Implemented by an HTTP client in the
/// browser and backed by API controllers on the server.
/// </summary>
public interface INuclearEvaluationApi
{
    // Grid views
    Task<DataResult<SeriesView>> GetSeriesViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<SampleView>> GetSampleViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<SubSampleView>> GetSubSampleViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<ApmView>> GetApmViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<ParticleView>> GetParticleViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<ProjectView>> GetProjectViews(DataQuery query, CancellationToken ct = default);
    Task<DataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(DataQuery query, CancellationToken ct = default);

    // Aggregates / lookups
    Task<SeriesCountsView> GetSeriesCounts(DataQuery query, CancellationToken ct = default);
    Task<List<int>> GetEnumFilterOptions(string entity, EnumFilterRequest request, CancellationToken ct = default);

    // Charts
    Task<List<IsotopeBinCounts>> GetProjectApmUraniumBinCounts(int projectId, CancellationToken ct = default);
    Task<List<IsotopeBinCounts>> GetProjectParticleUraniumBinCounts(int projectId, CancellationToken ct = default);

    // Series CRUD
    Task<int> CreateSeries(SeriesView seriesView, CancellationToken ct = default);
    Task UpdateSeries(SeriesView seriesView, CancellationToken ct = default);
    Task DeleteSeries(IReadOnlyCollection<int> seriesIds, CancellationToken ct = default);
    Task<List<SampleView>> GetSamplesForSeries(int seriesId, CancellationToken ct = default);

    // Project mutations
    Task UpdateProjectField(ProjectFieldUpdate update, CancellationToken ct = default);
    Task UpdateProjectSeries(ProjectSeriesUpdate update, CancellationToken ct = default);

    // Preset filters
    Task<List<PresetFilter>> GetPresetFilters(CancellationToken ct = default);
    Task<int> CreatePresetFilter(PresetFilter filter, CancellationToken ct = default);
    Task UpdatePresetFilter(PresetFilter filter, CancellationToken ct = default);
    Task DeletePresetFilter(int id, CancellationToken ct = default);

    // Name availability (inline validation)
    Task<bool> IsProjectNameAvailable(string name, int excludeId, CancellationToken ct = default);
    Task<bool> IsPresetFilterNameAvailable(string name, int excludeId, CancellationToken ct = default);

    // STEM preview
    Task<OperationOutcome> UploadStemPreviewFile(Guid sessionId, Guid fileId, string fileName, Stream content, CancellationToken ct = default);
    Task DeleteStemPreviewFile(Guid sessionId, Guid fileId, CancellationToken ct = default);

    // Proof-of-work captcha
    Task<CaptchaStatus> GetCaptchaStatus(CancellationToken ct = default);
    Task<CaptchaChallenge> GetCaptchaChallenge(CancellationToken ct = default);
    Task<CaptchaStatus> VerifyCaptcha(CaptchaSolution solution, CancellationToken ct = default);
}
