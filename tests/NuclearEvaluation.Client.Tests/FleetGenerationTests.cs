using System.Net;
using System.Text;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Fleet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public sealed class FleetGenerationTests : TestBase
{
    [Fact]
    public void CalendarDistinguishesReportedValuesAndUsesLeapFebruaryForDailyAverage()
    {
        using HttpClient http = RegisterSnapshot();
        TestContext.JSInterop.SetupModule("./scripts/fleetGeneration.js");

        IRenderedComponent<FleetGeneration> page = TestContext.Render<FleetGeneration>();
        page.WaitForElement(".generation-heatmap");

        Assert.Equal("-1", page.Find("button.generation-negative").TextContent);
        Assert.Equal("0", page.Find("button.generation-zero").TextContent);
        Assert.Contains(
            "Arizona · January",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );

        page.FindAll(".generation-selectors select")[1].Change("1");
        Assert.Contains(
            "29 GWh",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "energy-emissions?generationGwh=29#emissions-estimate",
            page.Find(".generation-detail .generation-toolbar a").GetAttribute("href")
        );

        page.Find(".generation-mode button:nth-child(2)").Click();
        Assert.Contains(
            "1 GWh/day",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "29 days",
            page.Find(".generation-detail .generation-toolbar p").TextContent,
            StringComparison.Ordinal
        );

        page.Find("button.generation-cell[data-row='1'][data-month='1']").Click();
        Assert.Contains(
            "Illinois · February",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "No state total",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Empty(page.FindAll(".generation-detail .generation-toolbar a"));
        Assert.Equal(
            ["2", "—"],
            page.FindAll(".generation-plant-table tbody td").Select(cell => cell.TextContent)
        );
        Assert.Equal(
            "true",
            page.Find("button.generation-missing[tabindex='0']").GetAttribute("aria-pressed")
        );
    }

    [Fact]
    public async Task ChangingTheStateQueryRevealsTheFirstState()
    {
        using HttpClient http = RegisterSnapshot();
        TestContext.Services.AddSupplyValueFromQueryProvider();
        BunitJSModuleInterop module = TestContext.JSInterop.SetupModule(
            "./scripts/fleetGeneration.js"
        );
        NavigationManager navigation = TestContext.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/fleet-generation?state=IL");
        IRenderedComponent<FleetGeneration> page = TestContext.Render<FleetGeneration>();

        page.WaitForAssertion(() =>
            Assert.Equal(1, Assert.Single(module.Invocations["reveal"]).Arguments[1])
        );
        await page.InvokeAsync(() => navigation.NavigateTo("/fleet-generation?state=AZ"));

        page.WaitForAssertion(() => Assert.Equal(2, module.Invocations["reveal"].Count));
        Assert.Equal(0, module.Invocations["reveal"][1].Arguments[1]);
        Assert.Contains(
            "Arizona · January",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void CsvIncludesEveryStateAndMonthInTheActiveMeasure()
    {
        using HttpClient http = RegisterSnapshot();
        TestContext.JSInterop.SetupModule("./scripts/fleetGeneration.js");
        IRenderedComponent<FleetGeneration> page = TestContext.Render<FleetGeneration>();
        IElement link = page.WaitForElement(".generation-download");
        const string dataPrefix = "data:text/csv;charset=utf-8;base64,";

        string[] DecodeCsv(IElement element) =>
            Encoding
                .UTF8.GetString(
                    Convert.FromBase64String(element.GetAttribute("href")![dataPrefix.Length..])
                )
                .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith(dataPrefix, link.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Equal("us-nuclear-generation-2024-monthly.csv", link.GetAttribute("download"));
        string[] monthly = DecodeCsv(link);
        Assert.Equal(1 + 2 * 12, monthly.Length);
        Assert.Equal("state_code,state,month,net_generation_gwh", monthly[0]);
        Assert.Equal("AZ,Arizona,2024-01,-1", monthly[1]);
        Assert.Equal("AZ,Arizona,2024-02,29", monthly[2]);
        Assert.Equal("AZ,Arizona,2024-03,0", monthly[3]);
        Assert.Equal("IL,Illinois,2024-02,", monthly[14]);
        Assert.Equal("IL,Illinois,2024-12,", monthly[^1]);

        page.Find(".generation-mode button:nth-child(2)").Click();
        link = page.Find(".generation-download");
        Assert.Equal("us-nuclear-generation-2024-daily-average.csv", link.GetAttribute("download"));
        string[] daily = DecodeCsv(link);
        Assert.Equal(monthly.Length, daily.Length);
        Assert.Equal("state_code,state,month,average_daily_net_generation_gwh_per_day", daily[0]);
        Assert.Equal("AZ,Arizona,2024-01,-0.032258", daily[1]);
        Assert.Equal("AZ,Arizona,2024-02,1", daily[2]);
        Assert.Equal(monthly[3], daily[3]);
        Assert.Equal(monthly[14], daily[14]);
    }

    [Fact]
    public async Task AssistantSelectsTheRealCalendarViewAndRejectsInvalidChangesAtomically()
    {
        using HttpClient http = RegisterSnapshot();
        BunitJSModuleInterop module = TestContext.JSInterop.SetupModule(
            "./scripts/fleetGeneration.js"
        );
        module
            .SetupVoid("reveal", _ => true)
            .SetException(new JSException("The optional scroll helper is unavailable."));
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<FleetGeneration> page = TestContext.Render<FleetGeneration>();
        page.WaitForAssertion(() => Assert.NotNull(workspace.Current));
        SiteAssistantTarget target = workspace.Current!;
        Assert.Equal(
            new SiteAssistantContext(
                AssistantPage.FleetGeneration,
                AssistantSurface.GenerationCalendar
            ),
            target.Context
        );

        Assert.True(
            await page.InvokeAsync(() => target.ApplyAsync(Select(month: 2, dailyAverage: true)))
        );
        Assert.Contains(
            "Arizona · February",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "1 GWh/day",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "1",
            page.Find(".generation-cell[aria-pressed=true]").GetAttribute("data-month")
        );

        Assert.True(await page.InvokeAsync(() => target.ApplyAsync(Select(state: "il"))));
        Assert.Contains(
            "Illinois · February",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "No state total",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "true",
            page.Find(".generation-mode button:nth-child(2)").GetAttribute("aria-pressed")
        );
        Assert.Same(target, workspace.Current);

        Assert.False(await page.InvokeAsync(() => target.ApplyAsync(Select("ZZ", 3, false))));
        Assert.False(await page.InvokeAsync(() => target.ApplyAsync(Select("AZ", 13, false))));
        Assert.Contains(
            "Illinois · February",
            page.Find(".generation-selection").TextContent,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "true",
            page.Find(".generation-mode button:nth-child(2)").GetAttribute("aria-pressed")
        );
    }

    [Theory]
    [InlineData(".generation-selectors label:first-child select", "1")]
    [InlineData(".generation-selectors label:nth-child(2) select", "1")]
    [InlineData(".generation-mode button:nth-child(2)", null)]
    [InlineData(".generation-cell[data-row='1'][data-month='1']", null)]
    public async Task ManualChangesInvalidatePendingCalendarActions(string selector, string? value)
    {
        using HttpClient http = RegisterSnapshot();
        TestContext.JSInterop.SetupModule("./scripts/fleetGeneration.js");
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<FleetGeneration> page = TestContext.Render<FleetGeneration>();
        page.WaitForAssertion(() => Assert.NotNull(workspace.Current));
        SiteAssistantTarget before = workspace.Current!;

        if (value is null)
            page.Find(selector).Click();
        else
            page.Find(selector).Change(value);

        Assert.NotSame(before, workspace.Current);
        string manualSelection = page.Find(".generation-selection").TextContent;
        Assert.False(await page.InvokeAsync(() => before.ApplyAsync(Select("AZ", 3, false))));
        Assert.Equal(manualSelection, page.Find(".generation-selection").TextContent);
    }

    static AssistantAction Select(
        string? state = null,
        int? month = null,
        bool? dailyAverage = null
    ) =>
        new(AssistantActionKind.SelectGenerationView)
        {
            GenerationSelection = new(state, month, dailyAverage),
        };

    HttpClient RegisterSnapshot()
    {
        HttpClient http = new(new SnapshotHandler()) { BaseAddress = new Uri("http://localhost/") };
        TestContext.Services.AddSingleton(http);
        return http;
    }

    sealed class SnapshotHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            FleetSnapshot snapshot = new(
                2024,
                [
                    new(1, "Desert Station", "AZ", 34, -112, [], [-1_000, 29_000, 0]),
                    new(2, "River Station", "IL", 38, -90, [], [50_000, 58_000]),
                    new(3, "Prairie Station", "IL", 40, -88, [], [55_000, null]),
                ]
            );
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(snapshot),
                }
            );
        }
    }
}
