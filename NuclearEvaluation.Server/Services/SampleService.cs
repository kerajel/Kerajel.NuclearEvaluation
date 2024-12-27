using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class SampleService : DbServiceBase, ISampleService
{
    public SampleService(NuclearEvaluationServerDbContext _dbContext) : base(_dbContext)
    {
    }

    public async Task<FilterDataResponse<SampleView>> GetSampleViews(FilterDataCommand<SampleView> command)
    {
        IQueryable<SampleView> baseQuery = _dbContext.SampleView;

        return await ExecuteQueryAsync(baseQuery, command);
    }
}