using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Shared.Models.Views;
using Shouldly;

namespace NuclearEvaluation.Client.Tests;

public class QueryFilterNormalizationTests
{
    [Fact]
    public void FilterWithFallback_ShouldHandleRadzenNullableNullCoalesceComparison()
    {
        IQueryable<ApmView> rows = new[]
        {
            new ApmView { Id = 1, U234 = null },
            new ApmView { Id = 2, U234 = 0.5m },
            new ApmView { Id = 3, U234 = 1.5m },
        }.AsQueryable();

        ApmView[] result = rows
            .FilterWithFallback("x => ((x.U234 ?? null) > 1)")
            .ToArray();

        result.Select(x => x.Id).ShouldBe([3]);
    }
}
