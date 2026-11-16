using System.Linq.Expressions;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class SeriesSchemaQueries(NuclearEvaluationServerDbContext db)
{
    public IQueryable<SeriesView> ApplyFilters(IQueryable<SeriesView> rows, SeriesGridQuery query)
    {
        if (!SeriesQuerySchema.IsValid(query))
            throw new ArgumentException(
                "The Series query contains an invalid filter or sort.",
                nameof(query)
            );

        ParameterExpression series = Expression.Parameter(typeof(SeriesView), "series");
        Expression? predicate = FieldPredicates(
            SeriesQuerySchema.GetFields(SeriesQueryEntity.Series),
            query.Filters,
            series
        );
        foreach (SeriesRelatedFilter filter in query.RelatedFilters)
        {
            Expression count = RelatedCount(filter.Rows, series);
            predicate = Combine(predicate, Compare(count, filter.Comparison, filter.Count));
        }

        return predicate is null
            ? rows
            : rows.Where(Expression.Lambda<Func<SeriesView, bool>>(predicate, series));
    }

    public Expression<Func<SeriesView, object?>> GetOrderKey(SeriesQueryOrder order)
    {
        ParameterExpression series = Expression.Parameter(typeof(SeriesView), "series");
        Expression key = order.Related is { } relation
            ? RelatedCount(relation, series)
            : Expression.Property(
                series,
                GetField(SeriesQueryEntity.Series, order.Field!).Property
            );
        return Expression.Lambda<Func<SeriesView, object?>>(
            Expression.Convert(key, typeof(object)),
            series
        );
    }

    Expression RelatedCount(SeriesRelatedRows relation, ParameterExpression series) =>
        relation.Entity switch
        {
            SeriesQueryEntity.Samples => Count(
                db.SampleView.Select(sample => new LinkedRow<SampleView>
                {
                    SeriesId = sample.SeriesId,
                    Row = sample,
                }),
                relation,
                series
            ),
            SeriesQueryEntity.SubSamples => Count(SubSampleRows(), relation, series),
            SeriesQueryEntity.Apms => Count(
                from subSample in SubSampleRows()
                join apm in db.ApmView on subSample.Row.Id equals apm.SubSampleId
                select new LinkedRow<ApmView> { SeriesId = subSample.SeriesId, Row = apm },
                relation,
                series
            ),
            SeriesQueryEntity.Particles => Count(
                from subSample in SubSampleRows()
                join particle in db.ParticleView on subSample.Row.Id equals particle.SubSampleId
                select new LinkedRow<ParticleView>
                {
                    SeriesId = subSample.SeriesId,
                    Row = particle,
                },
                relation,
                series
            ),
            SeriesQueryEntity.Projects => Count(
                from link in db.ProjectViewSeriesView
                join project in db.ProjectView on link.ProjectId equals project.Id
                select new LinkedRow<ProjectView> { SeriesId = link.SeriesId, Row = project },
                relation,
                series
            ),
            _ => throw new ArgumentException(
                "The related entity is not supported.",
                nameof(relation)
            ),
        };

    IQueryable<LinkedRow<SubSampleView>> SubSampleRows() =>
        from sample in db.SampleView
        join subSample in db.SubSampleView on sample.Id equals subSample.SampleId
        select new LinkedRow<SubSampleView> { SeriesId = sample.SeriesId, Row = subSample };

    static MethodCallExpression Count<T>(
        IQueryable<LinkedRow<T>> rows,
        SeriesRelatedRows relation,
        ParameterExpression series
    )
        where T : class
    {
        ParameterExpression link = Expression.Parameter(typeof(LinkedRow<T>), "link");
        Expression linkedSeries = Expression.Property(link, nameof(LinkedRow<T>.SeriesId));
        Expression seriesId = Expression.Property(series, nameof(SeriesView.Id));
        Expression matchSeries = Expression.Equal(linkedSeries, seriesId);
        Expression? filters = FieldPredicates(
            SeriesQuerySchema.GetFields(relation.Entity),
            relation.Filters,
            Expression.Property(link, nameof(LinkedRow<T>.Row))
        );

        return Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Count),
            [typeof(LinkedRow<T>)],
            rows.Expression,
            Expression.Quote(
                Expression.Lambda<Func<LinkedRow<T>, bool>>(Combine(filters, matchSeries), link)
            )
        );
    }

    internal static Expression? FieldPredicates(
        IReadOnlyList<SeriesQueryField> fields,
        IReadOnlyList<SeriesFieldFilter> filters,
        Expression row
    )
    {
        Expression? predicate = null;
        foreach (SeriesFieldFilter filter in filters)
        {
            SeriesQueryField field = SeriesQuerySchema.FindField(fields, filter.Field)
                ?? throw new ArgumentException(
                    "The query field is not supported.",
                    nameof(filters)
                );
            if (!SeriesQuerySchema.TryReadValue(field, filter, out object? value))
                throw new ArgumentException(
                    "The query contains an invalid field comparison.",
                    nameof(filters)
                );

            Expression member = Expression.Property(row, field.Property);
            predicate = Combine(predicate, Compare(member, filter.Comparison, value));
        }
        return predicate;
    }

    static SeriesQueryField GetField(SeriesQueryEntity entity, string name) =>
        SeriesQuerySchema.FindField(entity, name)
        ?? throw new ArgumentException("The query field is not supported.", nameof(name));

    static Expression Combine(Expression? left, Expression right) =>
        left is null ? right : Expression.AndAlso(left, right);

    static Expression Compare(Expression member, SeriesComparison comparison, object? value)
    {
        Expression operand = value is null
            ? Expression.Constant(null, member.Type)
            : CapturedValue(value, member.Type);

        return comparison switch
        {
            SeriesComparison.Equal or SeriesComparison.IsNull => Expression.Equal(member, operand),
            SeriesComparison.NotEqual or SeriesComparison.IsNotNull => Expression.NotEqual(
                member,
                operand
            ),
            SeriesComparison.GreaterThan => Expression.GreaterThan(member, operand),
            SeriesComparison.GreaterOrEqual => Expression.GreaterThanOrEqual(member, operand),
            SeriesComparison.LessThan => Expression.LessThan(member, operand),
            SeriesComparison.LessOrEqual => Expression.LessThanOrEqual(member, operand),
            SeriesComparison.Contains => Expression.Call(
                member,
                nameof(string.Contains),
                [],
                operand
            ),
            SeriesComparison.StartsWith => Expression.Call(
                member,
                nameof(string.StartsWith),
                [],
                operand
            ),
            SeriesComparison.EndsWith => Expression.Call(
                member,
                nameof(string.EndsWith),
                [],
                operand
            ),
            _ => throw new ArgumentException(
                "The comparison is not supported.",
                nameof(comparison)
            ),
        };
    }

    static Expression CapturedValue(object value, Type type)
    {
        Expression<Func<object>> capture = () => value;
        return Expression.Convert(capture.Body, type);
    }

    sealed class LinkedRow<T>
        where T : class
    {
        public int SeriesId { get; init; }
        public required T Row { get; init; }
    }
}
