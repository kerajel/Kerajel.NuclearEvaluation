using Bunit;
using NSubstitute;
using NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using Radzen;
using Shouldly;
using System.Reflection;

namespace NuclearEvaluation.Client.Tests;

public class QueryBuilderFilterTests : TestBase
{
    [Fact]
    public async Task RestoredPresetFilter_ShouldProduceFilterString()
    {
        IRenderedComponent<SampleQueryBuilderFilter> component = TestContext.Render<SampleQueryBuilderFilter>(
            parameters => parameters.Add(p => p.Visible, true));
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
            ]);

        await component.InvokeAsync(() => component.Instance.PresetFilterEntry = entry);

        string? filterString = component.Instance.FilterString;

        filterString.ShouldNotBeNullOrWhiteSpace();
        filterString.ShouldContain("Sample.Sequence");
        filterString.ShouldContain("Contains");
    }

    [Fact]
    public async Task QueryBuilderCard_WhenPresetSelected_ShouldIncludeFilterInPresetBox()
    {
        IRenderedComponent<QueryBuilderCard> component = TestContext.Render<QueryBuilderCard>();
        PresetFilter presetFilter = CreateSampleSequencePreset("sobaker-2");
        MethodInfo selectPreset = typeof(QueryBuilderCard).GetMethod(
            "OnPresetFilterSelected",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

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
            DefaultWaitForStateTimeout);
        Api.ClearReceivedCalls();
        PresetFilter presetFilter = CreateSampleSequencePreset("sobaker");
        MethodInfo selectPreset = typeof(QueryBuilderCard).GetMethod(
            "OnPresetFilterSelected",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await component.InvokeAsync(async () =>
        {
            Task task = (Task)selectPreset.Invoke(component.Instance, [presetFilter])!;
            await task;
        });

        component.WaitForAssertion(
            () => Api.Received().GetSeriesViews(
                Arg.Is<DataQuery>(query => HasSampleSequencePresetFilter(query)),
                Arg.Any<CancellationToken>()),
            DefaultWaitForStateTimeout);
    }

    [Fact]
    public async Task PresetFilterDropDown_WhenChanged_ShouldEmitSelectedPreset()
    {
        PresetFilter selectedPreset = new()
        {
            Id = 2,
            Name = "sobaker-2",
        };
        PresetFilter? emittedPreset = null;
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([selectedPreset]);
        IRenderedComponent<PresetFilterDropDown> component = TestContext.Render<PresetFilterDropDown>(
            parameters => parameters.Add(
                p => p.OnPresetFilterSelected,
                presetFilter => emittedPreset = presetFilter));
        MethodInfo onDropDownChange = typeof(PresetFilterDropDown).GetMethod(
            "OnDropDownChange",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

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
        PresetFilter selectedPreset = new()
        {
            Id = 2,
            Name = "sobaker-2",
        };
        PresetFilter emittedPreset = selectedPreset;
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([selectedPreset]);
        IRenderedComponent<PresetFilterDropDown> component = TestContext.Render<PresetFilterDropDown>(
            parameters => parameters.Add(
                p => p.OnPresetFilterSelected,
                presetFilter => emittedPreset = presetFilter));
        MethodInfo onDropDownChange = typeof(PresetFilterDropDown).GetMethod(
            "OnDropDownChange",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        PropertyInfo activeFilterId = typeof(PresetFilterDropDown).GetProperty(
            "ActiveFilterId",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await component.InvokeAsync(async () =>
        {
            Task selectTask = (Task)onDropDownChange.Invoke(component.Instance, [selectedPreset.Id])!;
            await selectTask;
            Task clearTask = (Task)onDropDownChange.Invoke(component.Instance, [null])!;
            await clearTask;
        });

        emittedPreset.Id.ShouldBe(0);
        activeFilterId.GetValue(component.Instance).ShouldBeNull();
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
                    ])
            ],
        };
    }

    static bool HasSampleSequencePresetFilter(DataQuery query)
    {
        string? filter = query.PresetFilterBox?.GetOrDefault(PresetFilterEntryType.Sample);
        return filter?.Contains("Sample.Sequence") == true;
    }
}
