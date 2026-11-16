using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

[McpServerToolType]
internal sealed class LocalSeriesTools
{
    static readonly string[] CountComparisons =
    [
        nameof(SeriesComparison.Equal),
        nameof(SeriesComparison.NotEqual),
        nameof(SeriesComparison.GreaterThan),
        nameof(SeriesComparison.GreaterOrEqual),
        nameof(SeriesComparison.LessThan),
        nameof(SeriesComparison.LessOrEqual),
    ];

    [McpServerTool(
        Name = SeriesMcpContract.DescribeSchemaToolName,
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Describe the Series grid and related row fields, value types, and supported comparisons."
    )]
    public SeriesSchemaDescription DescribeSchema() => DescribeGridSchema();

    [McpServerTool(
        Name = SeriesMcpContract.BuildQueryToolName,
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description("Validate a typed filter, sort, and limit for applying to the Series grid.")]
    public LocalSeriesToolResult BuildQuery(
        [Description("The complete Series grid query to apply.")] SeriesGridQuery query
    )
    {
        if (query is null || !IsValid(query))
            throw new McpException("The proposed Series grid query is invalid.");

        return new(query);
    }

    internal static bool IsValid(SeriesGridQuery query) =>
        Validator.TryValidateObject(
            query,
            new ValidationContext(query),
            validationResults: null,
            validateAllProperties: true
        ) && SeriesQuerySchema.IsValid(query);

    internal static SeriesSchemaDescription DescribeGridSchema() =>
        new(Enum.GetValues<SeriesQueryEntity>().Select(DescribeEntity).ToArray(), CountComparisons);

    static SeriesEntityDescription DescribeEntity(SeriesQueryEntity entity) =>
        new(
            entity.ToString(),
            entity switch
            {
                SeriesQueryEntity.Series => "Series",
                SeriesQueryEntity.Samples => "Series > Samples",
                SeriesQueryEntity.SubSamples => "Series > Samples > SubSamples",
                SeriesQueryEntity.Apms => "Series > Samples > SubSamples > APMs",
                SeriesQueryEntity.Particles => "Series > Samples > SubSamples > Particles",
                SeriesQueryEntity.Projects => "Series > Project links > Projects",
                _ => throw new ArgumentOutOfRangeException(nameof(entity)),
            },
            SeriesQuerySchema.GetFields(entity).Select(DescribeField).ToArray()
        );

    internal static SeriesFieldDescription DescribeField(SeriesQueryField field)
    {
        Type type = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
        return new SeriesFieldDescription(
            field.Name,
            type.IsEnum ? "Enum" : type.Name,
            field.AllowsNull,
            type.IsEnum ? Enum.GetNames(type) : [],
            field.Operators.Select(comparison => comparison.ToString()).ToArray()
        );
    }
}
