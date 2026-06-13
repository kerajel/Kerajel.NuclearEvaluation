using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Filters;
using Radzen;

namespace NuclearEvaluation.Client.Services;

public static class DataQueryExtensions
{
    /// <summary>Projects a Radzen grid load request into the serializable query the API expects.</summary>
    public static DataQuery ToDataQuery(
        this LoadDataArgs args,
        PresetFilterBox? presetFilterBox = null,
        int? projectId = null,
        bool decayCorrected = false,
        Guid? stemSessionId = null,
        IEnumerable<int>? priorityIds = null)
    {
        return new DataQuery
        {
            Filter = string.IsNullOrWhiteSpace(args.Filter) ? null : args.Filter,
            OrderBy = string.IsNullOrWhiteSpace(args.OrderBy) ? null : args.OrderBy,
            Skip = args.Skip,
            Top = args.Top,
            PresetFilterBox = presetFilterBox,
            ProjectId = projectId,
            DecayCorrected = decayCorrected,
            StemSessionId = stemSessionId,
            PriorityIds = priorityIds?.ToList(),
        };
    }
}
