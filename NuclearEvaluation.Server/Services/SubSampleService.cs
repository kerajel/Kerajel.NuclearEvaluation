using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Views;
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