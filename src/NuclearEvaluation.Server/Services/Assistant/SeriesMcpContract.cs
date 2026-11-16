using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal static class SeriesMcpContract
{
    internal const string BuildQueryToolName = "build_series_grid_query";
    internal const string DescribeSchemaToolName = "describe_series_grid_schema";
}

internal sealed record LocalSeriesToolResult(SeriesGridQuery Query);

internal sealed record SeriesSchemaDescription(
    IReadOnlyList<SeriesEntityDescription> Entities,
    IReadOnlyList<string> CountComparisons
);

internal sealed record SeriesEntityDescription(
    string Entity,
    string RelationshipPath,
    IReadOnlyList<SeriesFieldDescription> Fields
);

internal sealed record SeriesFieldDescription(
    string Name,
    string ValueType,
    bool AllowsNull,
    IReadOnlyList<string> EnumValues,
    IReadOnlyList<string> Comparisons
);
