using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Contexts;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.SharedServices.Services;

public class SampleService : DbServiceBase, ISampleService
{
    public SampleService(NuclearEvaluationServerDbContext _dbContext) : base(_dbContext)
    {
    }

    public async Task<FilterDataResponse<SampleView>> GetSampleViews(FilterDataCommand<SampleView> command)
    {
        IQueryable<SampleView> baseQuery = _dbContext.SampleView;

        return await ExecuteQuery(baseQuery, command);
    }
}