using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Contexts;

namespace NuclearEvaluation.SharedServices.Services;

public class ParticleService : DbServiceBase, IParticleService
{
    public ParticleService(NuclearEvaluationServerDbContext _dbContext) : base(_dbContext)
    {
    }

    public async Task<FilterDataResponse<ParticleView>> GetParticleViews(FilterDataCommand<ParticleView> command)
    {
        IQueryable<ParticleView> baseQuery;
        int? projectId;

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
}