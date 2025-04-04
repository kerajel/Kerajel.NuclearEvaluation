using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Data.Context;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Server.Services.DB;
using NuclearEvaluation.Server.Interfaces.Data;

namespace NuclearEvaluation.Server.Services.Data;

public class ParticleService : DbServiceBase, IParticleService
{
    readonly ILogger<ParticleService> _logger;

    public ParticleService(
        NuclearEvaluationServerDbContext _dbContext,
        ILogger<ParticleService> logger) : base(_dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<ParticleView>> GetParticleViews(FetchDataCommand<ParticleView> command)
    {
        IQueryable<ParticleView> baseQuery;
        int? projectId;

        try
        {
            switch (command.QueryKind)
            {
                case QueryKind.DecayCorrected:
                    projectId = command.GetRequiredArgument<int>(FilterDataCommand.ArgKeys.ProjectId);
                    baseQuery = _dbContext.ProjectDecayCorrectedParticleView
                                .Where(x => x.ProjectId == projectId);
                    break;

                default:
                    projectId = command.TryGetArgumentOrDefault<int?>(FilterDataCommand.ArgKeys.ProjectId);
                    baseQuery = _dbContext.ParticleView;
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
            return FetchDataResult<ParticleView>.Faulted(ex);
        }
    }
}