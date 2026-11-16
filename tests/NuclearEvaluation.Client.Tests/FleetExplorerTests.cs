using System.Globalization;
using System.Net;
using System.Text;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Shared.Fleet;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Tests;

public sealed class FleetExplorerTests : TestBase
{
    [Theory]
    [InlineData("garbage", "999999999999", "Central Station")]
    [InlineData("-5", "invalid", "Central Station")]
    [InlineData("2", "2", "River Station")]
    [InlineData("2", "999", "River Station")]
    public void InvalidIdsAndSelfComparisonStillOpenFleetExplorer(
        string plant,
        string comparison,
        string selected
    )
    {
        using HttpClient http = RegisterFleet("/fleet?plant=" + plant + "&compare=" + comparison);
        IRenderedComponent<FleetExplorer> page = TestContext.Render<FleetExplorer>();

        Assert.Equal(selected, page.WaitForElement("#fleet-selected-name").TextContent);
        Assert.Equal(selected, page.Find(".fleet-series li").TextContent);
        Assert.Empty(page.FindAll(".fleet-copy-link"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComparisonDeepLinkRestoresBothPlantsAndCanBeCopied(bool clipboardAvailable)
    {
        const string route = "/fleet?plant=2&compare=3";
        const string url = "http://localhost" + route;
        using HttpClient http = RegisterFleet(route);
        JSRuntimeInvocationHandler copy = TestContext.JSInterop.SetupVoid(
            "navigator.clipboard.writeText",
            url
        );
        if (clipboardAvailable)
            copy.SetVoidResult();
        else
            copy.SetException(new JSException("Clipboard write blocked."));

        IRenderedComponent<FleetExplorer> page = TestContext.Render<FleetExplorer>();
        page.WaitForElement(".fleet-copy-link").Click();

        Assert.Equal(
            ["River Station", "Desert Station"],
            page.FindAll(".fleet-series li").Select(row => row.TextContent)
        );
        Assert.Single(copy.Invocations);
        if (clipboardAvailable)
            Assert.Equal("Comparison link copied.", page.Find(".fleet-share-status").TextContent);
        else
        {
            IElement fallback = page.Find("#fleet-share-link");
            Assert.Equal(url, fallback.GetAttribute("value"));
            Assert.True(fallback.HasAttribute("readonly"));
        }
    }

    [Fact]
    public void FleetViewsUseGregorianMonthLabelsUnderSaudiArabicCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            using HttpClient http = RegisterFleet("/fleet");
            TestContext.JSInterop.SetupModule("./scripts/fleetGeneration.js");
            IRenderedComponent<FleetExplorer> explorer = TestContext.Render<FleetExplorer>();
            IRenderedComponent<FleetGeneration> calendar = TestContext.Render<FleetGeneration>();

            Assert.Equal(
                "Jan",
                explorer.WaitForElement(".fleet-monthly-data tbody th").TextContent
            );
            Assert.Equal(
                "Jan",
                calendar.WaitForElement(".generation-heatmap thead th:nth-child(2)").TextContent
            );
            Assert.Contains(
                "January",
                calendar.Find(".generation-selection").TextContent,
                StringComparison.Ordinal
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CapacityLegendUsesTheWholeFleetMaximumAfterFiltering()
    {
        using HttpClient http = RegisterFleet("/fleet");
        IRenderedComponent<FleetExplorer> page = TestContext.Render<FleetExplorer>();
        page.WaitForElement(".fleet-map-marker");
        Assert.Empty(page.FindAll(".fleet-capacity-legend"));

        page.Find(".fleet-map-panel .fleet-map-modes button:last-child").Click();
        string[] expected = ["250 MW", "500 MW", "1,000 MW"];
        Assert.Equal(
            expected,
            page.FindAll(".fleet-capacity-legend li").Select(row => row.TextContent.Trim())
        );

        page.Find(".fleet-state-filter select").Change("AZ");
        Assert.Equal(
            expected,
            page.FindAll(".fleet-capacity-legend li").Select(row => row.TextContent.Trim())
        );
        Assert.Contains(
            "--fleet-fraction:0.9",
            Assert.Single(page.FindAll(".fleet-map-marker")).GetAttribute("style"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void StateRankingFiltersBothViewsAndKeepsTwoPlantComparisonAcrossStates()
    {
        using HttpClient http = RegisterFleet("/fleet");

        IRenderedComponent<FleetExplorer> page = TestContext.Render<FleetExplorer>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".fleet-map-marker").Count));

        Assert.Contains(
            "Illinois",
            page.Find(".fleet-rank-state").TextContent,
            StringComparison.Ordinal
        );
        page.Find(".fleet-rank-state").Click();
        Assert.Equal(2, page.FindAll(".fleet-map-marker").Count);
        Assert.Equal(2, page.FindAll(".fleet-list tbody tr").Count);
        Assert.Single(page.FindAll(".fleet-map-state-active"));
        page.Find("button.fleet-map-marker[title^='River Station']").Click();

        page.Find(".fleet-state-filter select").Change("AZ");
        Assert.Single(page.FindAll(".fleet-list tbody tr"));
        page.Find("button.fleet-map-marker[title^='Desert Station']").Click();
        Assert.Contains(
            "River Station",
            page.Find(".fleet-pick:first-child").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Desert Station",
            page.Find(".fleet-pick:nth-child(2)").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "River Station",
            page.Find(".fleet-series").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Desert Station",
            page.Find(".fleet-series").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "energy-emissions?generationGwh=240#emissions-estimate",
            page.Find(".fleet-metric-note a").GetAttribute("href")
        );

        page.Find(".fleet-rank .fleet-map-modes button:nth-child(2)").Click();
        Assert.Contains(
            "Arizona",
            page.Find(".fleet-rank-state").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal("true", page.Find(".fleet-rank-state").GetAttribute("aria-pressed"));
    }

    HttpClient RegisterFleet(string route)
    {
        HttpClient http = new(new SnapshotHandler()) { BaseAddress = new Uri("http://localhost/") };
        TestContext.Services.AddSingleton(http);
        TestContext.Services.AddSupplyValueFromQueryProvider();
        TestContext.ComponentFactories.AddStub<RadzenChart>();
        TestContext.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        return http;
    }

    sealed class SnapshotHandler : HttpMessageHandler
    {
        const string Geometry = """
            {
              "features": [
                {
                  "properties": { "name": "Illinois" },
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-91,36],[-87,36],[-87,42],[-91,42],[-91,36]]]
                  }
                },
                {
                  "properties": { "name": "Arizona" },
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-115,31],[-109,31],[-109,37],[-115,37],[-115,31]]]
                  }
                }
              ]
            }
            """;

        static readonly FleetSnapshot Snapshot = new(
            2024,
            [
                new(1, "Central Station", "IL", 40, -89, [new("1", 1_000)], Months(35_000)),
                new(2, "River Station", "IL", 38, -90, [new("1", 600)], Months(20_000)),
                new(3, "Desert Station", "AZ", 34, -112, [new("1", 900)], Months(120_000)),
            ]
        );

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            HttpContent? content = request.RequestUri?.AbsolutePath switch
            {
                "/" + FleetSnapshot.AssetPath => JsonContent.Create(Snapshot),
                "/data/fleet-us-states.json" => new StringContent(
                    Geometry,
                    Encoding.UTF8,
                    "application/json"
                ),
                _ => null,
            };

            HttpResponseMessage response = new(
                content is null ? HttpStatusCode.NotFound : HttpStatusCode.OK
            )
            {
                Content = content,
            };
            return Task.FromResult(response);
        }

        static decimal?[] Months(decimal generationMwh) =>
            Enumerable.Repeat<decimal?>(generationMwh, 12).ToArray();
    }
}
