using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Shared.Services;

public class ApmService : DbServiceBase, IApmService
{
    private readonly ILogger<ApmService> _logger;

    public ApmService(
        NuclearEvaluationServerDbContext dbContext,
        ILogger<ApmService> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<ApmView>> GetApmViews(FetchDataCommand<ApmView> command)
    {
        IQueryable<ApmView> baseQuery;
        int? projectId;

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "");
            return FetchDataResult<ApmView>.Faulted(ex);
        }
    }
}