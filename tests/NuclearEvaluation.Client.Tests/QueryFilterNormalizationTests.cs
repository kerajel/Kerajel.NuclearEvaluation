using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Shared.Models.Views;
using Shouldly;

namespace NuclearEvaluation.Client.Tests;

public class QueryFilterNormalizationTests
{
    [Theory]
    [InlineData("x => ((x.U234 ?? null) > 1)")]
    [InlineData("x => ((((x == null) ? null : x.U234) ?? null) > 1)")]
    public void FilterWithFallback_ShouldHandleRadzenNullableNullCoalesceComparison(string filter)
    {
        IQueryable<ApmView> rows = new[]
        {
            new ApmView { Id = 1, U234 = null },
            new ApmView { Id = 2, U234 = 0.5m },
            new ApmView { Id = 3, U234 = 1.5m },
        }.AsQueryable();

        ApmView[] result = rows.FilterWithFallback(filter).ToArray();

        result.Select(x => x.Id).ShouldBe([3]);
    }
}

public class LiteralFilterTests
{
    [Theory]
    [InlineData("a==b")]
    [InlineData("default(NuclearEvaluation.Shared.Enums.SampleType)")]
    [InlineData("(NuclearEvaluation.Shared.Enums.SampleType)4")]
    [InlineData("default(bool)")]
    [InlineData("(x.U234 ?? null)")]
    [InlineData("quoted \\\" == text")]
    public void NormalizationPreservesQuotedFilterValues(string value)
    {
        IQueryable<ApmView> rows = new[]
        {
            new ApmView { Id = 1, Comment = value },
            new ApmView { Id = 2, Comment = "other" },
        }.AsQueryable();
        string literal = System.Text.Json.JsonSerializer.Serialize(value);
        Assert.Equal(1, rows.FilterWithFallback($"Comment == {literal}").Single().Id);
    }
}
