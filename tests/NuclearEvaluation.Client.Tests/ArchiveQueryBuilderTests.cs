using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Shared.Radiation;
using NuclearEvaluation.Client.Shared.Radiochemistry;

namespace NuclearEvaluation.Client.Tests;

public class ArchiveQueryBuilderTests : TestBase
{
    [Fact]
    public void RadiochemistryExampleCanBeCollapsedAndReset() =>
        CheckBuilder<RemDbBuilder>("rem-query-editor", "CS-137");

    [Fact]
    public void RadiationExampleCanBeCollapsedAndReset() =>
        CheckBuilder<RadNetBuilder>("radnet-query-editor", "Example station");

    void CheckBuilder<TBuilder>(string editorId, string expectedRow)
        where TBuilder : IComponent
    {
        using QueryHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);
        IRenderedComponent<TBuilder> builder = TestContext.Render<TBuilder>();

        builder
            .FindAll("button")
            .Single(button => button.TextContent.Trim() == "Run example")
            .Click();
        builder.WaitForAssertion(() =>
            Assert.Contains(expectedRow, builder.Find("tbody").TextContent)
        );
        JsonElement example = Assert.Single(handler.Queries);
        Assert.NotEmpty(Filters(example));
        string[] values = builder
            .FindAll("fieldset input")
            .Select(input => input.GetAttribute("value") ?? "")
            .ToArray();

        string toggle = $"button[aria-controls='{editorId}']";
        builder.Find(toggle).Click();
        Assert.True(builder.Find($"#{editorId}").HasAttribute("hidden"));
        Assert.Equal("false", builder.Find(toggle).GetAttribute("aria-expanded"));
        Assert.Contains(expectedRow, builder.Find("tbody").TextContent);

        builder.Find(toggle).Click();
        Assert.Equal("true", builder.Find(toggle).GetAttribute("aria-expanded"));
        Assert.Equal(
            values,
            builder.FindAll("fieldset input").Select(input => input.GetAttribute("value") ?? "")
        );
        Assert.Single(handler.Queries);

        builder
            .FindAll("button")
            .Single(button => button.TextContent.Trim() == "Reset filters")
            .Click();
        builder.WaitForAssertion(() => Assert.Equal(2, handler.Queries.Count));
        JsonElement reset = handler.Queries[1];
        Assert.Empty(Filters(reset));
        Assert.Equal(
            example.GetProperty("entity").GetString(),
            reset.GetProperty("entity").GetString()
        );
        Assert.Equal(JsonValueKind.Null, reset.GetProperty("sortField").ValueKind);
        Assert.False(reset.GetProperty("descending").GetBoolean());
        Assert.Equal(0, reset.GetProperty("skip").GetInt32());
        Assert.Empty(builder.FindAll("fieldset"));
        Assert.Equal("", builder.Find("input[aria-label='Preset name']").GetAttribute("value"));
    }

    [Fact]
    public void ResetDoesNotReloadTheAutomaticRadiochemistryExampleOnParentRender()
    {
        using QueryHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);
        IRenderedComponent<RemDbBuilder> builder = TestContext.Render<RemDbBuilder>(parameters =>
            parameters.Add(component => component.RunExampleOnLoad, true)
        );
        builder.WaitForAssertion(() => Assert.Single(handler.Queries));
        builder
            .FindAll("button")
            .Single(button => button.TextContent.Trim() == "Reset filters")
            .Click();

        builder.Render(parameters => parameters.Add(component => component.RunExampleOnLoad, true));

        builder.WaitForAssertion(() => Assert.Equal(2, handler.Queries.Count));
        Assert.Empty(builder.FindAll("fieldset"));
        Assert.Empty(Filters(handler.Queries[^1]));
    }

    static JsonElement[] Filters(JsonElement query) =>
        query.GetProperty("filters").Deserialize<JsonElement[]>() ?? [];

    sealed class QueryHandler : HttpMessageHandler
    {
        public List<JsonElement> Queries { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            using JsonDocument query = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken)
            );
            Queries.Add(query.RootElement.Clone());
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        Items = new[]
                        {
                            new { StationName = "Example station", Nuclide = "CS-137" },
                        },
                        TotalCount = 1,
                    }
                ),
            };
        }
    }
}
