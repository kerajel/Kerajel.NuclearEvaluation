using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class SubSampleService : DbServiceBase, ISubSampleService
{
    public SubSampleService(NuclearEvaluationServerDbContext _dbContext) : base(_dbContext)
    {
    }

    public async Task<FilterDataResponse<SubSampleView>> GetSubSampleViews(FilterDataCommand<SubSampleView> command)
    {
        IQueryable<SubSampleView> baseQuery = _dbContext.SubSampleView;

        return await ExecuteQuery(baseQuery, command);
    }
}