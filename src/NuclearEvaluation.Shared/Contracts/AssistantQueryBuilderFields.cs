using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Shared.Contracts;

public static class AssistantQueryBuilderFields
{
    public const int MaxFilters = 20;

    static readonly Dictionary<SeriesQueryEntity, IReadOnlyList<string>> Fields = new()
    {
        [SeriesQueryEntity.Series] =
        [
            nameof(SeriesView.Id),
            nameof(SeriesView.SeriesType),
            nameof(SeriesView.CreatedAt),
            nameof(SeriesView.SgasComment),
            nameof(SeriesView.WorkingPaperLink),
            nameof(SeriesView.IsDu),
            nameof(SeriesView.IsNu),
            nameof(SeriesView.AnalysisCompleteDate),
            nameof(SeriesView.SampleExternalCodes),
            nameof(SeriesView.SampleCount),
        ],
        [SeriesQueryEntity.Samples] =
        [
            nameof(SampleView.Sequence),
            nameof(SampleView.ExternalCode),
            nameof(SampleView.SeriesId),
            nameof(SampleView.SampleClass),
            nameof(SampleView.SampleType),
            nameof(SampleView.SamplingDate),
            nameof(SampleView.SubSampleCount),
            nameof(SampleView.Latitude),
            nameof(SampleView.Longitude),
        ],
        [SeriesQueryEntity.SubSamples] =
        [
            nameof(SubSampleView.Sequence),
            nameof(SubSampleView.ExternalCode),
            nameof(SubSampleView.ScreeningDate),
            nameof(SubSampleView.IsFromLegacySystem),
            nameof(SubSampleView.ActivityNotes),
            nameof(SubSampleView.TrackingNumber),
        ],
        [SeriesQueryEntity.Apms] =
        [
            nameof(ApmView.Id),
            nameof(ApmView.U234),
            nameof(ApmView.ErU234),
            nameof(ApmView.U235),
            nameof(ApmView.ErU235),
            nameof(ApmView.U236),
            nameof(ApmView.ErU236),
            nameof(ApmView.U238),
            nameof(ApmView.ErU238),
            nameof(ApmView.Comment),
        ],
        [SeriesQueryEntity.Particles] =
        [
            nameof(ParticleView.ParticleExternalId),
            nameof(ParticleView.U234),
            nameof(ParticleView.ErU234),
            nameof(ParticleView.U235),
            nameof(ParticleView.ErU235),
            nameof(ParticleView.AnalysisDate),
            nameof(ParticleView.IsNu),
            nameof(ParticleView.LaboratoryCode),
            nameof(ParticleView.Comment),
        ],
    };

    public static IReadOnlyList<string> GetFields(SeriesQueryEntity entity) =>
        Fields.TryGetValue(entity, out IReadOnlyList<string>? fields) ? fields : [];

    public static bool IsAllowed(SeriesQueryEntity entity, string field) =>
        GetFields(entity).Contains(field, StringComparer.Ordinal);
}
