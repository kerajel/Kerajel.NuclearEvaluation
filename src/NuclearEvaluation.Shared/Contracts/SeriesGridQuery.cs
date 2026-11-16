using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NuclearEvaluation.Shared.Enums;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<SeriesGridSort>))]
public enum SeriesGridSort
{
    None,
    QcSampleCount,
    SampleCount,
    CreatedAt,
    AnalysisCompleteDate,
    Id,
}

public sealed record SeriesGridQuery : IValidatableObject
{
    public IReadOnlyList<SeriesFieldFilter> Filters { get; init; } = [];
    public IReadOnlyList<SeriesRelatedFilter> RelatedFilters { get; init; } = [];
    public SeriesQueryOrder? Order { get; init; }

    [Range(1, DataQuery.MaxPageSize)]
    public int? Limit { get; init; }

    [EnumDataType(typeof(SeriesGridSort))]
    public SeriesGridSort SortBy { get; init; }

    public bool Descending { get; init; }

    [EnumDataType(typeof(SeriesType))]
    public SeriesType? Type { get; init; }

    [Range(1, int.MaxValue)]
    public int? SeriesId { get; init; }

    public bool? IsDu { get; init; }
    public bool? IsNu { get; init; }
    public bool? IsComplete { get; init; }
    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedBefore { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinSamples { get; init; }

    [Range(0, int.MaxValue)]
    public int? MaxSamples { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinQcSamples { get; init; }

    [Range(0, int.MaxValue)]
    public int? MaxQcSamples { get; init; }

    [StringLength(100)]
    public string? CommentContains { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedFrom >= CreatedBefore)
        {
            yield return new(
                "The creation date range is empty.",
                [nameof(CreatedFrom), nameof(CreatedBefore)]
            );
        }

        if (MinSamples > MaxSamples)
        {
            yield return new(
                "The sample count range is empty.",
                [nameof(MinSamples), nameof(MaxSamples)]
            );
        }

        if (MinQcSamples > MaxQcSamples)
        {
            yield return new(
                "The QC sample count range is empty.",
                [nameof(MinQcSamples), nameof(MaxQcSamples)]
            );
        }
    }
}
