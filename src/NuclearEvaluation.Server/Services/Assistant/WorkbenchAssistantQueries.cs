using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Server.Services.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal static class WorkbenchAssistantQueries
{
    internal static WorkbenchAssistantGridSchema? FindGrid(
        SiteAssistantContext context,
        IReadOnlyList<WorkbenchAssistantGridSchema>? grids
    ) =>
        context.Page == AssistantPage.Workbench
            ? grids?.FirstOrDefault(grid =>
                grid.Surface == context.Surface
                && grid.ResultEntity == context.WorkbenchResultEntity
            )
            : null;

    internal static int MaxGroups(WorkbenchAssistantGridSchema grid) =>
        Math.Min(grid.Fields.Count, RadNetCatalog.MaxGroups);

    internal static bool IsValid(SiteAssistantContext context, AssistantWorkbenchGridQuery? query)
    {
        WorkbenchAssistantGridSchema? grid = FindGrid(context, WorkbenchAssistantCatalog.Grids);
        if (
            grid is null
            || query?.Filters is not { } groups
            || groups.Count > MaxGroups(grid)
            || query.PageSize is < 1 or > RadNetCatalog.MaxPageSize
            || (groups.Count == 0 && query.SortField is null && query.PageSize is null)
        )
            return false;

        if (query.SortField is not null && !grid.Fields.Any(field => field.Name == query.SortField))
            return false;

        HashSet<string> columns = new(StringComparer.Ordinal);
        foreach (ColumnFilterGroup group in groups)
        {
            if (
                group?.Conditions is not { Count: > 0 and <= ColumnFilterGroup.MaxConditions }
                || grid.Fields.FirstOrDefault(field => field.Name == group.Field) is not { } field
                || !columns.Add(field.Name)
                || group.Conditions.Any(condition => !IsConditionValid(field, condition))
            )
                return false;
        }

        try
        {
            int top = query.PageSize ?? 50;
            switch (grid.Surface)
            {
                case AssistantSurface.WorkbenchSeries:
                    WorkbenchSeriesSort sort = query.SortField is { } name
                        ? Enum.Parse<WorkbenchSeriesSort>(name)
                        : WorkbenchSeriesSort.Name;
                    WorkbenchStore.ValidateSeriesColumnFilters(sort, groups);
                    return true;
                case AssistantSurface.WorkbenchProjectObservations:
                case AssistantSurface.WorkbenchArchiveObservations:
                    RadNetQueryPlan.Compile(
                        new(
                            RadNetEntity.Observation,
                            groups.Select(AsRadNetFilter).ToArray(),
                            query.SortField,
                            query.Descending,
                            Top: top
                        )
                    );
                    return true;
                case AssistantSurface.WorkbenchQueryResults when grid.ResultEntity is { } entity:
                    WorkbenchQueryPlan.Compile(
                        new(
                            Entity: entity,
                            SortField: query.SortField,
                            Descending: query.Descending,
                            Top: top,
                            ResultFilterGroups: groups
                        )
                    );
                    return true;
                case AssistantSurface.WorkbenchImportRows:
                    ImportReviewQueryPlan.Validate(
                        new(query.SortField, query.Descending, Top: top, Filters: groups)
                    );
                    return true;
                default:
                    return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    static bool IsConditionValid(WorkbenchAssistantField field, ColumnFilterCondition? condition)
    {
        if (condition is null)
            return false;
        if (condition.Operator is RadNetOperator.IsMissing or RadNetOperator.IsPresent)
            return field.Nullable && condition.Value is null;
        if (
            condition.Value is not { Length: > 0 and <= 200 } value
            || string.IsNullOrWhiteSpace(value)
        )
            return false;

        return field.Values is not { Count: > 0 } values
            || (
                condition.Operator is RadNetOperator.Equal or RadNetOperator.NotEqual
                && values.Contains(value, StringComparer.Ordinal)
            );
    }

    static RadNetFilterGroup AsRadNetFilter(ColumnFilterGroup group) =>
        new(
            RadNetCatalog.Fields.Single(field => field.Name == group.Field).Entity,
            group.MatchAny,
            group
                .Conditions.Select(condition => new RadNetCondition(
                    group.Field,
                    condition.Operator,
                    condition.Value
                ))
                .ToArray()
        );
}
