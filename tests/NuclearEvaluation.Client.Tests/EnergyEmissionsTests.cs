using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Pages;

namespace NuclearEvaluation.Client.Tests;

public class EnergyEmissionsTests : TestBase
{
    [Fact]
    public void ComparesTwoSourcesAtTheSameGenerationAndConvertsUnits()
    {
        IRenderedComponent<EnergyEmissions> page = TestContext.Render<EnergyEmissions>();
        Assert.Equal(
            ["13 t CO₂e", "486 t CO₂e"],
            page.FindAll(".energy-estimate dd:not([aria-hidden])")
                .Select(element => element.TextContent)
        );

        page.Find("#energy-generation-unit").Change("MegawattHours");
        page.Find("#energy-generation").Input("2");

        Assert.Equal(
            ["26 kg CO₂e", "972 kg CO₂e"],
            page.FindAll(".energy-estimate dd:not([aria-hidden])")
                .Select(element => element.TextContent)
        );
        Assert.Equal("946 kg CO₂e", page.Find(".energy-scenario-summary strong").TextContent);
    }

    [Fact]
    public void ReportsTheSameEstimateForDifferentSourcesWithEqualMedians()
    {
        IRenderedComponent<EnergyEmissions> page = TestContext.Render<EnergyEmissions>();

        page.Find("#energy-source-b").Change("1");

        Assert.Equal("Wind", page.FindAll(".energy-estimate dt")[1].TextContent);
        Assert.Equal("Same estimate", page.Find(".energy-scenario-summary strong").TextContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000001")]
    public void RequiresAValidGenerationAmount(string input)
    {
        IRenderedComponent<EnergyEmissions> page = TestContext.Render<EnergyEmissions>();

        page.Find("#energy-generation").Input(input);

        Assert.Empty(page.FindAll(".energy-estimates"));
        Assert.NotNull(page.Find(".energy-scenario-empty"));
        Assert.Equal("true", page.Find("#energy-generation").GetAttribute("aria-invalid"));
    }

    [Theory]
    [InlineData("2.5", "2.5")]
    [InlineData("invalid", "1")]
    [InlineData("-2.5", "1")]
    public void AcceptsOnlyValidGigawattHourPrefill(string supplied, string expected)
    {
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/energy-emissions?generationGwh={supplied}#emissions-estimate");

        IRenderedComponent<EnergyEmissions> page = TestContext.Render<EnergyEmissions>();

        Assert.Equal(expected, page.Find("#energy-generation").GetAttribute("value"));
        Assert.Equal(
            "GigawattHours",
            page.Find("#energy-generation-unit option[selected]")?.GetAttribute("value")
        );
    }
}
