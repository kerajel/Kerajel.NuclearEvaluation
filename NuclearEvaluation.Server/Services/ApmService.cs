using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class ApmService : DbServiceBase, IApmService
{
    public ApmService(NuclearEvaluationServerDbContext _dbContext) : base(_dbContext)
    {
    }

    public async Task<FilterDataResponse<ApmView>> GetApmViews(FilterDataCommand<ApmView> command)
    {
        IQueryable<ApmView> baseQuery;
        int? projectId;

        switch (command.QueryKind)
        {
            case QueryKind.DecayCorrected:
                projectId = command.GetRequiredArgument<int>(FilterDataCommand.ArgKeys.ProjectId);
                baseQuery = _dbContext.ProjectDecayCorrectedApmView
                            .Where(x => x.ProjectId == projectId);
                break;

            default:
                projectId = command.TryGetArgumentOrDefault<int?>(FilterDataCommand.ArgKeys.ProjectId);
                baseQuery = _dbContext.ApmView;
                if (projectId.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.SubSample.Sample.Series.ProjectSeries.Any(x => x.ProjectId == projectId.Value));
                }
                break;
        }

        return await ExecuteQuery(baseQuery, command);
    }
}