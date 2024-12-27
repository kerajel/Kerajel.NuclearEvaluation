using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Plotting;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

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

        return await ExecuteQueryAsync(baseQuery, command);
    }
}