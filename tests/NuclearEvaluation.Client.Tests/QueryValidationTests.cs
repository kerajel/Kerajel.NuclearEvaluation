using System.ComponentModel.DataAnnotations;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Filters;

namespace NuclearEvaluation.Client.Tests;

public class QueryValidationTests
{
    static bool Valid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), [], true);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(int.MaxValue)]
    public void QueryRejectsUnboundedPages(int top) =>
        Assert.False(Valid(new DataQuery { Top = top }));

    [Fact]
    public void QueryDefaultsAndSupportedPageSizesAreValid()
    {
        Assert.True(Valid(new DataQuery()));
        Assert.True(
            Valid(
                new DataQuery
                {
                    Skip = 100,
                    Top = 500,
                    ProjectId = 1,
                }
            )
        );
    }

    [Fact]
    public void DecayCorrectionRequiresProjectContext() =>
        Assert.False(Valid(new DataQuery { DecayCorrected = true }));

    [Fact]
    public void ProjectMutationRejectsDuplicateAndInvalidIds()
    {
        Assert.False(Valid(new ProjectSeriesUpdate { ProjectId = 1, SeriesIds = [1, 1] }));
        Assert.False(Valid(new ProjectSeriesUpdate { ProjectId = 1, SeriesIds = [-1] }));
        Assert.True(Valid(new ProjectSeriesUpdate { ProjectId = 1, SeriesIds = [1, 2] }));
    }

    [Fact]
    public void ProjectNameCannotBypassUiValidation() =>
        Assert.False(
            Valid(
                new ProjectFieldUpdate
                {
                    ProjectId = 1,
                    Field = ProjectField.Name,
                    StringValue = " ",
                }
            )
        );

    [Fact]
    public void BlankPresetEntriesDoNotTriggerQueryBuilderJoins()
    {
        PresetFilterBox filters = new();
        filters.Set(PresetFilterEntryType.Sample, " ");
        Assert.False(filters.HasFilter());
    }

    [Fact]
    public void DuplicatePresetTypesCannotBeSaved()
    {
        PresetFilter filter = new()
        {
            Name = "Valid preset",
            Entries =
            [
                new() { PresetFilterEntryType = PresetFilterEntryType.Sample },
                new() { PresetFilterEntryType = PresetFilterEntryType.Sample },
            ],
        };
        Assert.False(Valid(filter));
    }
}
