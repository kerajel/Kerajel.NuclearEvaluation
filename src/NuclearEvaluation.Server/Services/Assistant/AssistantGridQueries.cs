using System.Linq.Expressions;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Assistant;

internal static class AssistantGridQueries
{
    static readonly IReadOnlyList<SeriesQueryField> StemFields = SeriesQuerySchema
        .FindScalarFields<StemPreviewEntryView>()
        .Where(field =>
            field.Name != nameof(StemPreviewEntryView.FileId)
            && field.Name != nameof(StemPreviewEntryView.FileName)
        )
        .ToArray();

    static readonly Dictionary<
        AssistantSurface,
        (Type RowType, IReadOnlyList<SeriesQueryField> Fields)
    > Grids = new()
    {
        [AssistantSurface.Series] = SqlGrid<SeriesView>(SeriesQueryEntity.Series),
        [AssistantSurface.Projects] = SqlGrid<ProjectView>(SeriesQueryEntity.Projects),
        [AssistantSurface.Samples] = SqlGrid<SampleView>(SeriesQueryEntity.Samples),
        [AssistantSurface.SubSamples] = SqlGrid<SubSampleView>(SeriesQueryEntity.SubSamples),
        [AssistantSurface.Apms] = SqlGrid<ApmView>(SeriesQueryEntity.Apms),
        [AssistantSurface.Particles] = SqlGrid<ParticleView>(SeriesQueryEntity.Particles),
        [AssistantSurface.StemPreview] = (typeof(StemPreviewEntryView), StemFields),
        [AssistantSurface.StemQuality] = (typeof(StemPreviewEntryView), StemFields),
    };

    static (Type, IReadOnlyList<SeriesQueryField>) SqlGrid<T>(SeriesQueryEntity entity) =>
        (typeof(T), SeriesQuerySchema.GetFields(entity));

    public static IReadOnlyList<SeriesQueryField> Fields(AssistantSurface surface) =>
        Grids.TryGetValue(surface, out (Type RowType, IReadOnlyList<SeriesQueryField> Fields) grid)
            ? grid.Fields
            : [];

    public static bool IsValid(AssistantGridQuery query)
    {
        IReadOnlyList<SeriesQueryField> fields = Fields(query.Grid);
        return query.Grid != AssistantSurface.Series
            && fields.Count > 0
            && (query.Limit is null or (>= 1 and <= DataQuery.MaxPageSize))
            && (
                query.SortField is null
                || SeriesQuerySchema.FindField(fields, query.SortField) is not null
            )
            && SeriesQuerySchema.AreFiltersValid(fields, query.Filters);
    }

    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> rows, AssistantGridQuery query)
        where T : class
    {
        VerifyQuery<T>(query);
        ParameterExpression row = Expression.Parameter(typeof(T), "row");
        Expression? predicate = SeriesSchemaQueries.FieldPredicates(
            Fields(query.Grid),
            query.Filters,
            row
        );
        return predicate is null
            ? rows
            : rows.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
    }

    public static IOrderedQueryable<T> ApplyOrder<T>(
        IQueryable<T> rows,
        AssistantGridQuery query,
        Expression<Func<T, object>> fallbackKey
    )
        where T : class
    {
        VerifyQuery<T>(query);
        if (query.SortField is null)
            return rows.OrderBy(fallbackKey);

        SeriesQueryField field = SeriesQuerySchema.FindField(Fields(query.Grid), query.SortField)!;
        ParameterExpression row = Expression.Parameter(typeof(T), "row");
        Expression<Func<T, object>> key = Expression.Lambda<Func<T, object>>(
            Expression.Convert(Expression.Property(row, field.Property), typeof(object)),
            row
        );
        return (query.Descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(
            fallbackKey
        );
    }

    static void VerifyQuery<T>(AssistantGridQuery query)
        where T : class
    {
        if (
            !Grids.TryGetValue(
                query.Grid,
                out (Type RowType, IReadOnlyList<SeriesQueryField> Fields) grid
            )
            || !grid.RowType.IsAssignableFrom(typeof(T))
            || !IsValid(query)
        )
            throw new ArgumentException(
                "The assistant query contains an invalid target, filter or sort.",
                nameof(query)
            );
    }
}
