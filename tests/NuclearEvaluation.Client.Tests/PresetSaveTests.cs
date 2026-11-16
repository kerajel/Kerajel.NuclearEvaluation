using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Tests;

public class PresetSaveTests : TestBase
{
    const string SavePreset =
        "button[aria-label='Save the current query builder filters as a preset.']";
    const string SaveEditor = "button[aria-label='Save the current query builder filters.']";
    const string DeletePreset = "button[aria-label='Delete the selected saved filter.']";
    const string OriginalName = "QC series draft";

    [Fact]
    public async Task ExampleLoadedDuringValidationPreventsTheOldSaveFromPosting()
    {
        IRenderedComponent<QueryBuilderCard> builder = await OpenNamedPreset();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> available = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.IsPresetFilterNameAvailable(OriginalName, 0, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return available.Task;
            });

        Task submission = builder
            .Find(SaveEditor)
            .TriggerEventAsync("onclick", new MouseEventArgs());
        await started.Task.WaitAsync(DefaultWaitForStateTimeout);
        LoadExample(builder, "Samples in NU series");
        available.SetResult(true);
        await submission.WaitAsync(DefaultWaitForStateTimeout);

        await Api.DidNotReceive()
            .CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        Assert.Null(FindSavedFilters(builder).Value);
        Assert.NotNull(builder.Find(SavePreset));
    }

    [Fact]
    public async Task PostedSaveAddsTheOriginalPresetWithoutSelectingItInTheNewDraft()
    {
        IRenderedComponent<QueryBuilderCard> builder = await OpenNamedPreset();
        TaskCompletionSource<PresetFilter> posted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource<int> created = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                posted.TrySetResult(call.Arg<PresetFilter>());
                return created.Task;
            });

        Task submission = builder
            .Find(SaveEditor)
            .TriggerEventAsync("onclick", new MouseEventArgs());
        PresetFilter snapshot = await posted.Task.WaitAsync(DefaultWaitForStateTimeout);
        Assert.True(builder.Find(SaveEditor).HasAttribute("disabled"));
        IRenderedComponent<RadzenButton> save = builder
            .FindComponents<RadzenButton>()
            .Single(button => button.Instance.Text == "Save");
        await save.InvokeAsync(() => save.Instance.Click.InvokeAsync(new MouseEventArgs()))
            .WaitAsync(DefaultWaitForStateTimeout);

        LoadExample(builder, "Samples in NU series");
        created.SetResult(73);
        await submission.WaitAsync(DefaultWaitForStateTimeout);

        await Api.Received(1)
            .CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>());
        Assert.Equal(OriginalName, snapshot.Name);
        Assert.Equal(
            [PresetFilterEntryType.Series, PresetFilterEntryType.Sample],
            snapshot.Entries.Select(entry => entry.PresetFilterEntryType)
        );
        RadzenDropDown<int?> dropdown = FindSavedFilters(builder);
        Assert.Null(dropdown.Value);
        PresetFilter saved = Assert.Single(dropdown.Data.Cast<PresetFilter>());
        Assert.Equal(73, saved.Id);
        Assert.Equal(OriginalName, saved.Name);
        Assert.NotNull(builder.Find(SavePreset));
    }

    [Fact]
    public async Task NewlySavedPresetCanBeDeletedWithoutReselectingIt()
    {
        IRenderedComponent<QueryBuilderCard> builder = await OpenNamedPreset();
        Api.CreatePresetFilter(Arg.Any<PresetFilter>(), Arg.Any<CancellationToken>()).Returns(73);
        await builder.Find(SaveEditor).TriggerEventAsync("onclick", new MouseEventArgs());
        Assert.Equal(73, FindSavedFilters(builder).Value);

        DialogService dialogs = builder
            .FindComponent<PresetFilterDropDown>()
            .Instance.DialogService;
        Task deletion = builder
            .Find(DeletePreset)
            .TriggerEventAsync("onclick", new MouseEventArgs());
        await builder.InvokeAsync(() => dialogs.Close(true));
        await deletion.WaitAsync(DefaultWaitForStateTimeout);

        await Api.Received(1).DeletePresetFilter(73, Arg.Any<CancellationToken>());
        RadzenDropDown<int?> dropdown = FindSavedFilters(builder);
        Assert.Empty(dropdown.Data.Cast<PresetFilter>());
        Assert.Null(dropdown.Value);
    }

    [Fact]
    public async Task DeleteKeepsTheConfirmedIdAndPreservesAnExampleDraftLoadedWhileItIsPending()
    {
        Api.GetPresetFilters(Arg.Any<CancellationToken>())
            .Returns([
                new() { Id = 7, Name = "First preset" },
                new() { Id = 8, Name = "Second preset" },
            ]);
        TaskCompletionSource<int> requested = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource removed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Api.DeletePresetFilter(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requested.TrySetResult(call.Arg<int>());
                return removed.Task;
            });

        IRenderedComponent<QueryBuilderCard> builder = TestContext.Render<QueryBuilderCard>();
        RadzenDropDown<int?> dropdown = FindSavedFilters(builder);
        await builder.InvokeAsync(() => dropdown.ValueChanged.InvokeAsync(7));
        DialogService dialogs = builder
            .FindComponent<PresetFilterDropDown>()
            .Instance.DialogService;
        Task deletion = builder
            .Find(DeletePreset)
            .TriggerEventAsync("onclick", new MouseEventArgs());

        await builder.InvokeAsync(() => dropdown.ValueChanged.InvokeAsync(8));
        await builder.InvokeAsync(() => dialogs.Close(true));
        int id = await requested.Task.WaitAsync(DefaultWaitForStateTimeout);
        LoadExample(builder, "Samples in NU series");
        removed.SetResult();
        await deletion.WaitAsync(DefaultWaitForStateTimeout);

        Assert.Equal(7, id);
        Assert.Equal(8, Assert.Single(dropdown.Data.Cast<PresetFilter>()).Id);
        Assert.Null(dropdown.Value);
        Assert.True(
            builder.Instance.GetPresetFilterBox().Filters.ContainsKey(PresetFilterEntryType.Series)
        );
    }

    async Task<IRenderedComponent<QueryBuilderCard>> OpenNamedPreset()
    {
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([]);
        Api.IsPresetFilterNameAvailable(OriginalName, 0, Arg.Any<CancellationToken>())
            .Returns(true);
        IRenderedComponent<QueryBuilderCard> builder = TestContext.Render<QueryBuilderCard>();
        LoadExample(builder, "Regular series with QC samples");
        builder.Find(SavePreset).Click();
        await builder
            .Find(".preset-filter-controls input[placeholder='Enter filter name']")
            .TriggerEventAsync("oninput", new ChangeEventArgs { Value = OriginalName });
        builder.WaitForAssertion(() =>
            Assert.False(builder.Find(SaveEditor).HasAttribute("disabled"))
        );
        return builder;
    }

    static void LoadExample(IRenderedComponent<QueryBuilderCard> builder, string name) =>
        builder
            .FindAll(".query-builder-examples button")
            .Single(button => button.TextContent == name)
            .Click();

    static RadzenDropDown<int?> FindSavedFilters(IRenderedComponent<QueryBuilderCard> builder) =>
        builder
            .FindComponent<PresetFilterDropDown>()
            .FindComponent<RadzenDropDown<int?>>()
            .Instance;
}
