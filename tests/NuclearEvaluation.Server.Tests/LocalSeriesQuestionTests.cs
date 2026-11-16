using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;

namespace NuclearEvaluation.Server.Tests;

public class LocalSeriesQuestionTests
{
    static readonly SeriesSchemaDescription Schema = new LocalSeriesTools().DescribeSchema();

    [Fact]
    public void ParsesDuAndSingularQcConditionBeforeTrailingOrder()
    {
        const string question =
            "Show me top 5 series where Is DU is false and there is at least 1 QC sample order by ID desc";

        SeriesGridQuery? query = LocalSeriesQuestion.Parse(question, Schema);
        Assert.NotNull(query);
        Assert.Equal(5, query.Limit);
        Assert.Equal(new SeriesQueryOrder("Id", null, true), query.Order);
        Assert.Equal(
            new SeriesFieldFilter("IsDu", SeriesComparison.Equal, "false"),
            Assert.Single(query.Filters)
        );
        SeriesRelatedFilter related = Assert.Single(query.RelatedFilters);
        Assert.Equal(SeriesQueryEntity.Samples, related.Rows.Entity);
        Assert.Equal(SeriesComparison.GreaterOrEqual, related.Comparison);
        Assert.Equal(1, related.Count);
        Assert.Equal(
            new SeriesFieldFilter("SampleType", SeriesComparison.Equal, "Qc"),
            Assert.Single(related.Rows.Filters)
        );
    }

    [Fact]
    public void ParsesNuComparisonWithAscendingOrder()
    {
        SeriesGridQuery? query = LocalSeriesQuestion.Parse(
            "Show the first 3 series where IsNU=true order by ID asc",
            Schema
        );
        Assert.NotNull(query);
        Assert.Equal(3, query.Limit);
        Assert.Equal(new SeriesQueryOrder("Id", null, false), query.Order);
        Assert.Equal(
            new SeriesFieldFilter("IsNu", SeriesComparison.Equal, "true"),
            Assert.Single(query.Filters)
        );
    }

    [Fact]
    public void PreservesInlineQcOrderAndSampleCountFilter()
    {
        SeriesGridQuery? query = LocalSeriesQuestion.Parse(
            "Show me the top 5 regular series by QC samples where the sample count is at least 3.",
            Schema
        );
        Assert.NotNull(query);
        Assert.Equal(5, query.Limit);
        Assert.Equal(SeriesType.Regular, query.Type);
        Assert.Equal(
            new SeriesFieldFilter("SampleCount", SeriesComparison.GreaterOrEqual, "3"),
            Assert.Single(query.Filters)
        );
        Assert.NotNull(query.Order?.Related);
        Assert.True(query.Order.Descending);
        Assert.Equal(SeriesQueryEntity.Samples, query.Order.Related.Entity);
        Assert.Equal(
            new SeriesFieldFilter("SampleType", SeriesComparison.Equal, "Qc"),
            Assert.Single(query.Order.Related.Filters)
        );
    }

    [Fact]
    public void ResolvesAFieldOnARelatedEntity()
    {
        SeriesGridQuery? query = LocalSeriesQuestion.Parse(
            "Show me series where there is a sample with External Code starts with 'ABC'",
            Schema
        );
        Assert.NotNull(query);
        SeriesRelatedFilter related = Assert.Single(query.RelatedFilters);
        Assert.Equal(SeriesQueryEntity.Samples, related.Rows.Entity);
        Assert.Equal(SeriesComparison.GreaterOrEqual, related.Comparison);
        Assert.Equal(1, related.Count);
        Assert.Equal(
            new SeriesFieldFilter("ExternalCode", SeriesComparison.StartsWith, "ABC"),
            Assert.Single(related.Rows.Filters)
        );
    }

    [Theory]
    [InlineData("Show me series where Is DU is true and Is DU is false")]
    [InlineData("Show me top 5 series by QC samples where Is DU is false order by ID desc")]
    [InlineData("Show me series where SGAS Comment contains test or Is DU is false")]
    public void RejectsContradictoryCriteria(string question) =>
        Assert.Null(LocalSeriesQuestion.Parse(question, Schema));

    [Fact]
    public void AfterADateExcludesThatEntireDay()
    {
        SeriesGridQuery? query = LocalSeriesQuestion.Parse(
            "Show me series where created after 2024-05-31",
            Schema
        );
        Assert.NotNull(query);
        Assert.Equal(
            new SeriesFieldFilter("CreatedAt", SeriesComparison.GreaterOrEqual, "2024-06-01"),
            Assert.Single(query.Filters)
        );
    }

    [Fact]
    public void UsesOnlyFieldsAdvertisedByTheSchema()
    {
        SeriesSchemaDescription limitedSchema = Schema with
        {
            Entities = Schema
                .Entities.Select(entity =>
                    entity with
                    {
                        Fields = entity.Fields.Where(field => field.Name != "IsDu").ToArray(),
                    }
                )
                .ToArray(),
        };

        Assert.Null(
            LocalSeriesQuestion.Parse("Show me series where Is_DU is false", limitedSchema)
        );
        Assert.NotNull(LocalSeriesQuestion.Parse("Show me series where Is_DU is false", Schema));
    }
}
