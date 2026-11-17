using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Evaluation;
using NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Models.QueryBuilder;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;
using Shouldly;

namespace NuclearEvaluation.Client.Tests;

public class QueryBuilderFilterTests : TestBase
{
    [Fact]
    public async Task RestoredPresetFilter_ShouldProduceFilterString()
    {
        IRenderedComponent<SampleQueryBuilderFilter> component =
            TestContext.Render<SampleQueryBuilderFilter>(parameters =>
                parameters.Add(p => p.Visible, true)
            );
        PresetFilterEntry entry = PresetFilterEntry.Create(
            PresetFilterEntryType.Sample,
            [
                new CompositeFilterDescriptor
                {
                    Property = "Sample.Sequence",
                    FilterValue = "b",
                    FilterOperator = FilterOperator.Contains,
                    LogicalFilterOperator = LogicalFilterOperator.And,
                },
            ]
        );

        await component.InvokeAsync(() => component.Instance.PresetFilterEntry = entry);

        string? filterString = component.Instance.FilterString;

        filterString.ShouldNotBeNullOrWhiteSpace();
        filterString.ShouldContain("Sample.Sequence");
        filterString.ShouldContain("Contains");
    }

    [Fact]
    public async Task RestoredPresetFilter_ShowsLegacyEnumValueInNestedGroup()
    {
        IRenderedComponent<SampleQueryBuilderFilter> component =
            TestContext.Render<SampleQueryBuilderFilter>(parameters =>
                parameters.Add(p => p.Visible, true)
            );
        PresetFilterEntry preset = PresetFilterEntry.Create(
            PresetFilterEntryType.Sample,
            [
                new CompositeFilterDescriptor
                {
                    Filters =
                    [
                        new CompositeFilterDescriptor
                        {
                            Property = "Sample.SampleType",
                            FilterOperator = FilterOperator.Equals,
                            FilterValue = SampleType.Qc,
                        },
                    ],
                },
            ]
        );
        PresetFilterEntry restored = new()
        {
            PresetFilterEntryType = PresetFilterEntryType.Sample,
            SerializedDescriptors = preset.SerializedDescriptors,
        };
        string expectedPredicate = preset.Descriptors.ToFilterString<SampleViewPresetFilter>(
            LogicalFilterOperator.And,
            FilterCaseSensitivity.Default
        );

        await component.InvokeAsync(() => component.Instance.PresetFilterEntry = restored);

        component
            .Find(".rz-datafilter-editor .rz-dropdown-label")
            .TextContent.Trim()
            .ShouldBe("Qc");
        component.Instance.FilterString.ShouldBe(expectedPredicate);
        PresetFilterEntry saved = new()
        {
            SerializedDescriptors = component.Instance.PresetFilterEntry.SerializedDescriptors,
        };
        saved.Descriptors.Single().Filters!.Single().FilterValue.ShouldBe((int)SampleType.Qc);
    }

    [Fact]
    public async Task QueryBuilderCard_WhenPresetSelected_ShouldIncludeFilterInPresetBox()
    {
        IRenderedComponent<QueryBuilderCard> component = TestContext.Render<QueryBuilderCard>();
        PresetFilter presetFilter = CreateSampleSequencePreset("sobaker-2");
        MethodInfo selectPreset = typeof(QueryBuilderCard).GetMethod(
            "OnPresetFilterSelected",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        await component.InvokeAsync(async () =>
        {
            Task task = (Task)selectPreset.Invoke(component.Instance, [presetFilter])!;
            await task;
        });

        PresetFilterBox presetFilterBox = component.Instance.GetPresetFilterBox();

        presetFilterBox.Filters.ShouldContainKey(PresetFilterEntryType.Sample);
        string sampleFilter = presetFilterBox.GetOrDefault(PresetFilterEntryType.Sample)!;
        sampleFilter.ShouldContain("Sample.Sequence");
    }

    [Fact]
    public async Task QueryBuilderCard_WhenPresetSelected_ShouldReloadActiveGridWithPresetFilter()
    {
        IRenderedComponent<QueryBuilderCard> component = TestContext.Render<QueryBuilderCard>();
        component.WaitForAssertion(
            () => Api.Received().GetSeriesViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>()),
            DefaultWaitForStateTimeout
        );
        Api.ClearReceivedCalls();
        PresetFilter presetFilter = CreateSampleSequencePreset("sobaker");
        MethodInfo selectPreset = typeof(QueryBuilderCard).GetMethod(
            "OnPresetFilterSelected",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        await component.InvokeAsync(async () =>
        {
            Task task = (Task)selectPreset.Invoke(component.Instance, [presetFilter])!;
            await task;
        });

        component.WaitForAssertion(
            () =>
                Api.Received()
                    .GetSeriesViews(
                        Arg.Is<DataQuery>(query => HasSampleSequencePresetFilter(query)),
                        Arg.Any<CancellationToken>()
                    ),
            DefaultWaitForStateTimeout
        );
    }

    [Fact]
    public async Task AssistantDraftReplacesSavedSelectionAndAppliesTypedFilters()
    {
        PresetFilter saved = new()
        {
            Id = 7,
            Name = "Reviewed series",
            Entries =
            [
                new PresetFilterEntry
                {
                    PresetFilterEntryType = PresetFilterEntryType.Series,
                    IsEnabled = true,
                    LogicalFilterOperator = LogicalFilterOperator.Or,
                },
            ],
        };
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([saved]);
        IRenderedComponent<QueryBuilderCard> component = TestContext.Render<QueryBuilderCard>();
        IRenderedComponent<RadzenDropDown<int?>> savedFilter = component
            .FindComponent<PresetFilterDropDown>()
            .FindComponent<RadzenDropDown<int?>>();
        await savedFilter.InvokeAsync(() =>
            savedFilter.Instance.ValueChanged.InvokeAsync(saved.Id)
        );
        Api.ClearReceivedCalls();

        AssistantAction action = new(AssistantActionKind.DraftQuery)
        {
            BuilderFilters =
            [
                new(
                    SeriesQueryEntity.Series,
                    new(nameof(SeriesView.IsDu), SeriesComparison.Equal, "false")
                ),
                new(
                    SeriesQueryEntity.Series,
                    new(nameof(SeriesView.SampleCount), SeriesComparison.GreaterOrEqual, "1")
                ),
                new(
                    SeriesQueryEntity.Samples,
                    new(
                        nameof(SampleView.SampleType),
                        SeriesComparison.Equal,
                        nameof(SampleType.Qc)
                    )
                ),
            ],
        };
        await component.InvokeAsync(async () =>
            (await component.Instance.ApplyAssistantAction(action)).ShouldBeTrue()
        );

        PresetFilterEntry series = component
            .FindComponent<SeriesQueryBuilderFilter>()
            .Instance.PresetFilterEntry;
        series.LogicalFilterOperator.ShouldBe(LogicalFilterOperator.And);
        series.Descriptors.Select(filter => filter.FilterValue).ShouldBe([false, 1]);
        PresetFilterEntry sample = component
            .FindComponent<SampleQueryBuilderFilter>()
            .Instance.PresetFilterEntry;
        sample.Descriptors.Single().FilterValue.ShouldBe((int)SampleType.Qc);
        savedFilter.Instance.Value.ShouldBeNull();
        saved
            .Entries.Single(entry => entry.PresetFilterEntryType == PresetFilterEntryType.Series)
            .LogicalFilterOperator.ShouldBe(LogicalFilterOperator.Or);
        await Api.Received()
            .GetSeriesViews(
                Arg.Is<DataQuery>(query =>
                    query.PresetFilterBox != null
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Series)
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Sample)
                ),
                Arg.Any<CancellationToken>()
            );
        await Api.DidNotReceive()
            .CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        await Api.DidNotReceive()
            .UpdatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        await Api.DidNotReceive().DeletePresetFilter(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExampleQueriesApplyUnsavedDraftsAndExpirePendingAssistantActions()
    {
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ProjectView>.Succeeded([], 0));
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/evaluation?tab=query-builder");
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<EvaluationTabs> evaluation = TestContext.Render<EvaluationTabs>();
        IRenderedComponent<QueryBuilderCard> builder = evaluation.FindComponent<QueryBuilderCard>();
        SiteAssistantTarget pending = workspace.Current!;

        void LoadExample(string title) =>
            builder
                .FindAll(".query-builder-examples button")
                .Single(button => button.TextContent == title)
                .Click();

        LoadExample("Regular series with QC samples");
        builder
            .FindComponent<SeriesQueryBuilderFilter>()
            .Instance.PresetFilterEntry.Descriptors.Single()
            .FilterValue.ShouldBe((int)SeriesType.Regular);
        builder
            .FindComponent<SampleQueryBuilderFilter>()
            .Instance.PresetFilterEntry.Descriptors.Single()
            .FilterValue.ShouldBe((int)SampleType.Qc);
        workspace.Current!.Context.ResultGrid.ShouldBe(AssistantSurface.Series);
        workspace.Current.ShouldNotBeSameAs(pending);
        await builder.InvokeAsync(async () =>
            (
                await pending.ApplyAsync(
                    new(AssistantActionKind.DraftQuery) { BuilderFilters = [] }
                )
            ).ShouldBeFalse()
        );
        await Api.Received()
            .GetSeriesViews(
                Arg.Is<DataQuery>(query =>
                    query.PresetFilterBox != null
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Series)
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Sample)
                ),
                Arg.Any<CancellationToken>()
            );

        LoadExample("Samples in NU series");
        workspace.Current!.Context.ResultGrid.ShouldBe(AssistantSurface.Samples);
        await Api.Received()
            .GetSampleViews(
                Arg.Is<DataQuery>(query =>
                    query.PresetFilterBox != null
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Series)
                ),
                Arg.Any<CancellationToken>()
            );

        LoadExample("NU particles with a U-234 value");
        workspace.Current!.Context.ResultGrid.ShouldBe(AssistantSurface.Particles);
        await Api.Received()
            .GetParticleViews(
                Arg.Is<DataQuery>(query =>
                    query.PresetFilterBox != null
                    && query.PresetFilterBox.Filters.ContainsKey(PresetFilterEntryType.Particle)
                ),
                Arg.Any<CancellationToken>()
            );
        await Api.DidNotReceive()
            .CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        await Api.DidNotReceive()
            .UpdatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        await Api.DidNotReceive().DeletePresetFilter(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssistantExampleCreatesAnUnsavedNamedParticleDraftWithAllLinkedFilters()
    {
        QueryBuilderExample example = QueryBuilderExample.Advanced;
        Api.IsPresetFilterNameAvailable(example.Title, 0, Arg.Any<CancellationToken>())
            .Returns(true);
        IRenderedComponent<QueryBuilderCard> builder = TestContext.Render<QueryBuilderCard>();
        builder.WaitForAssertion(() => Assert.True(builder.Instance.IsAssistantReady));
        Assert.Equal(AssistantSurface.Series, builder.Instance.AssistantResultSurface);

        await builder.InvokeAsync(async () =>
            Assert.True(
                await builder.Instance.ApplyAssistantAction(
                    new(AssistantActionKind.ShowQueryBuilderExample)
                )
            )
        );

        Assert.Equal(AssistantSurface.Particles, builder.Instance.AssistantResultSurface);
        Assert.Contains(
            $"{example.Title} · Unsaved draft",
            builder.Find(".ne-grid-caption[role='status']").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(builder.FindAll(".query-builder-examples button"), button =>
            button.TextContent == example.Title
        );
        IRenderedComponent<PresetFilterDropDown> presets =
            builder.FindComponent<PresetFilterDropDown>();
        Assert.Null(presets.FindComponent<RadzenDropDown<int?>>().Instance.Value);
        PresetFilter draft = presets.Instance.GetAppliedPresetFilter();
        Assert.Equal(0, draft.Id);
        Assert.Equal(example.Title, draft.Name);
        Assert.All(draft.Entries, entry => Assert.True(entry.IsEnabled));
        Assert.Equal(
            [
                "Apm.U235",
                "Particle.ErU234",
                "Particle.U234",
                "Sample.SampleType",
                "Series.AnalysisCompleteDate",
                "Series.SeriesType",
                "SubSample.IsFromLegacySystem",
            ],
            draft
                .Entries.SelectMany(entry => entry.Descriptors)
                .Select(descriptor => descriptor.Property)
                .Order(StringComparer.Ordinal)
        );
        await Api.Received()
            .GetParticleViews(
                Arg.Is<DataQuery>(query =>
                    query.PresetFilterBox != null && query.PresetFilterBox.Filters.Count == 5
                ),
                Arg.Any<CancellationToken>()
            );

        builder
            .Find("button[aria-label='Save the current query builder filters as a preset.']")
            .Click();
        Assert.Equal(example.Title, presets.FindComponent<RadzenTextBox>().Instance.Value);
        await Api.DidNotReceive()
            .CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        await Api.DidNotReceive()
            .UpdatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManualFilterAndPresetEditsInvalidatePendingAssistantDraft()
    {
        PresetFilter saved = CreateSampleSequencePreset("saved query");
        saved.Id = 9;
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([saved]);
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ProjectView>.Succeeded([], 0));
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/evaluation?tab=query-builder");

        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<EvaluationTabs> evaluation = TestContext.Render<EvaluationTabs>();
        IRenderedComponent<QueryBuilderCard> builder = evaluation.FindComponent<QueryBuilderCard>();
        SiteAssistantTarget beforeManualEdit = workspace.Current!;
        beforeManualEdit.Context.Surface.ShouldBe(AssistantSurface.QueryBuilder);
        AssistantAction draft = new(AssistantActionKind.DraftQuery) { BuilderFilters = [] };

        IRenderedComponent<RadzenDataFilter<SampleViewPresetFilter>> editor = builder.FindComponent<
            RadzenDataFilter<SampleViewPresetFilter>
        >();
        await editor.InvokeAsync(() =>
            editor.Instance.AddFilter(
                new CompositeFilterDescriptor
                {
                    Property = "Sample.Sequence",
                    FilterOperator = FilterOperator.Equals,
                    FilterValue = "my latest input",
                }
            )
        );

        SiteAssistantTarget beforePresetSelection = workspace.Current!;
        beforePresetSelection.ShouldNotBeSameAs(beforeManualEdit);
        await builder.InvokeAsync(async () =>
            (await beforeManualEdit.ApplyAsync(draft)).ShouldBeFalse()
        );
        editor.Instance.Filters.Single().FilterValue.ShouldBe("my latest input");

        IRenderedComponent<RadzenDropDown<int?>> preset = builder
            .FindComponent<PresetFilterDropDown>()
            .FindComponent<RadzenDropDown<int?>>();
        await preset.InvokeAsync(() => preset.Instance.ValueChanged.InvokeAsync(saved.Id));

        workspace.Current.ShouldNotBeSameAs(beforePresetSelection);
        await builder.InvokeAsync(async () =>
            (await beforePresetSelection.ApplyAsync(draft)).ShouldBeFalse()
        );
        preset.Instance.Value.ShouldBe(saved.Id);
    }

    [Fact]
    public async Task PresetFilterDropDown_WhenChanged_ShouldEmitSelectedPreset()
    {
        PresetFilter selectedPreset = new() { Id = 2, Name = "sobaker-2" };
        PresetFilter? emittedPreset = null;
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([selectedPreset]);
        IRenderedComponent<PresetFilterDropDown> component =
            TestContext.Render<PresetFilterDropDown>(parameters =>
                parameters.Add(
                    p => p.OnPresetFilterSelected,
                    presetFilter => emittedPreset = presetFilter
                )
            );
        MethodInfo onDropDownChange = typeof(PresetFilterDropDown).GetMethod(
            "OnDropDownChange",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        await component.InvokeAsync(async () =>
        {
            Task task = (Task)onDropDownChange.Invoke(component.Instance, [selectedPreset.Id])!;
            await task;
        });

        emittedPreset.ShouldBeSameAs(selectedPreset);
    }

    [Fact]
    public async Task PresetFilterDropDown_WhenCleared_ShouldEmitBlankPresetAndClearSelectedId()
    {
        PresetFilter selectedPreset = new() { Id = 2, Name = "sobaker-2" };
        PresetFilter emittedPreset = selectedPreset;
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([selectedPreset]);
        IRenderedComponent<PresetFilterDropDown> component =
            TestContext.Render<PresetFilterDropDown>(parameters =>
                parameters.Add(
                    p => p.OnPresetFilterSelected,
                    presetFilter => emittedPreset = presetFilter
                )
            );
        MethodInfo onDropDownChange = typeof(PresetFilterDropDown).GetMethod(
            "OnDropDownChange",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        PropertyInfo activeFilterId = typeof(PresetFilterDropDown).GetProperty(
            "ActiveFilterId",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        await component.InvokeAsync(async () =>
        {
            Task selectTask = (Task)
                onDropDownChange.Invoke(component.Instance, [selectedPreset.Id])!;
            await selectTask;
            Task clearTask = (Task)onDropDownChange.Invoke(component.Instance, [null])!;
            await clearTask;
        });

        emittedPreset.Id.ShouldBe(0);
        activeFilterId.GetValue(component.Instance).ShouldBeNull();
    }

    [Fact]
    public async Task CheckboxEditsKeepAppliedResultsUntilApplyQuery()
    {
        IRenderedComponent<QueryBuilderCard> component = TestContext.Render<QueryBuilderCard>();
        component.WaitForAssertion(() =>
            Api.Received().GetSeriesViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
        );
        IRenderedComponent<SampleQueryBuilderFilter> sample =
            component.FindComponent<SampleQueryBuilderFilter>();
        await sample.InvokeAsync(() =>
            sample.Instance.PresetFilterEntry = PresetFilterEntry.Create(
                PresetFilterEntryType.Sample,
                [
                    new CompositeFilterDescriptor
                    {
                        Property = "Sample.ExternalCode",
                        FilterOperator = FilterOperator.Equals,
                        FilterValue = "001",
                    },
                ]
            )
        );
        IRenderedComponent<RadzenCheckBox<bool>> checkbox = component.FindComponents<
            RadzenCheckBox<bool>
        >()[1];
        IRenderedComponent<RadzenButton> apply = component
            .FindComponents<RadzenButton>()
            .Single(x => x.Instance.Text == "Apply query");

        Api.ClearReceivedCalls();
        await checkbox.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
        await Api.DidNotReceive()
            .GetSeriesViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>());
        Assert.True(component.Instance.GetPresetFilterBox().IsEmpty());
        await apply.InvokeAsync(() => apply.Instance.Click.InvokeAsync());
        Assert.Contains(
            "001",
            component.Instance.GetPresetFilterBox().GetOrDefault(PresetFilterEntryType.Sample)
        );
        await Api.Received()
            .GetSeriesViews(
                Arg.Is<DataQuery>(x => x.PresetFilterBox != null && x.PresetFilterBox.HasFilter()),
                Arg.Any<CancellationToken>()
            );

        Api.ClearReceivedCalls();
        await checkbox.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));
        await Api.DidNotReceive()
            .GetSeriesViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>());
        Assert.True(component.Instance.GetPresetFilterBox().HasFilter());
        PresetFilter draft = component
            .FindComponent<PresetFilterDropDown>()
            .Instance.GetAppliedPresetFilter();
        Assert.False(Assert.Single(draft.Entries).IsEnabled);
        await apply.InvokeAsync(() => apply.Instance.Click.InvokeAsync());
        Assert.True(component.Instance.GetPresetFilterBox().IsEmpty());
        await Api.Received()
            .GetSeriesViews(
                Arg.Is<DataQuery>(x => x.PresetFilterBox != null && x.PresetFilterBox.IsEmpty()),
                Arg.Any<CancellationToken>()
            );
    }

    static PresetFilter CreateSampleSequencePreset(string name)
    {
        return new()
        {
            Id = 1,
            Name = name,
            Entries =
            [
                PresetFilterEntry.Create(
                    PresetFilterEntryType.Sample,
                    [
                        new CompositeFilterDescriptor
                        {
                            Property = "Sample.Sequence",
                            FilterValue = "b",
                            FilterOperator = FilterOperator.Contains,
                            LogicalFilterOperator = LogicalFilterOperator.And,
                        },
                    ]
                ),
            ],
        };
    }

    static bool HasSampleSequencePresetFilter(DataQuery query)
    {
        string? filter = query.PresetFilterBox?.GetOrDefault(PresetFilterEntryType.Sample);
        return filter?.Contains("Sample.Sequence") == true;
    }
}
