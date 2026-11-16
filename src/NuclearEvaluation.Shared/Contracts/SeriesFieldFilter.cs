using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<SeriesQueryEntity>))]
public enum SeriesQueryEntity
{
    Series,
    Samples,
    SubSamples,
    Apms,
    Particles,
    Projects,
}

[JsonConverter(typeof(JsonStringEnumConverter<SeriesComparison>))]
public enum SeriesComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    IsNull,
    IsNotNull,
}

public sealed record SeriesFieldFilter(string Field, SeriesComparison Comparison, string? Value);

public sealed record SeriesRelatedRows(
    SeriesQueryEntity Entity,
    IReadOnlyList<SeriesFieldFilter> Filters
);

public sealed record SeriesRelatedFilter(
    SeriesRelatedRows Rows,
    SeriesComparison Comparison,
    int Count
);

public sealed record SeriesQueryOrder(string? Field, SeriesRelatedRows? Related, bool Descending);
