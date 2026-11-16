using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Shared.Contracts;

public sealed record QueryBuilderExample(
    string Title,
    SeriesQueryEntity ResultEntity,
    IReadOnlyList<AssistantBuilderFilter> Filters
)
{
    public const string AdvancedSlug = "qc-particle-review";

    public static QueryBuilderExample Advanced { get; } =
        new(
            "QC particle review",
            SeriesQueryEntity.Particles,
            [
                new(
                    SeriesQueryEntity.Series,
                    new(
                        nameof(SeriesView.SeriesType),
                        SeriesComparison.Equal,
                        nameof(SeriesType.Regular)
                    )
                ),
                new(
                    SeriesQueryEntity.Series,
                    new(nameof(SeriesView.AnalysisCompleteDate), SeriesComparison.IsNotNull, null)
                ),
                new(
                    SeriesQueryEntity.Samples,
                    new(
                        nameof(SampleView.SampleType),
                        SeriesComparison.Equal,
                        nameof(SampleType.Qc)
                    )
                ),
                new(
                    SeriesQueryEntity.SubSamples,
                    new(nameof(SubSampleView.IsFromLegacySystem), SeriesComparison.Equal, "false")
                ),
                new(
                    SeriesQueryEntity.Apms,
                    new(nameof(ApmView.U235), SeriesComparison.IsNotNull, null)
                ),
                new(
                    SeriesQueryEntity.Particles,
                    new(nameof(ParticleView.U234), SeriesComparison.IsNotNull, null)
                ),
                new(
                    SeriesQueryEntity.Particles,
                    new(nameof(ParticleView.ErU234), SeriesComparison.LessOrEqual, "0.2")
                ),
            ]
        );
}
