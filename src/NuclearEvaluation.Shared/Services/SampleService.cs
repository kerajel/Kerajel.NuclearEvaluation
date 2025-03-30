using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Shared.Services;

public class SampleService : DbServiceBase, ISampleService
{
    readonly ILogger<SampleService> _logger;

    public SampleService(
        NuclearEvaluationServerDbContext _dbContext,
        ILogger<SampleService> logger) : base(_dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<SampleView>> GetSampleViews(FetchDataCommand<SampleView> command)
    {
        try
        {
            IQueryable<SampleView> baseQuery = _dbContext.SampleView;
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "");
            return FetchDataResult<SampleView>.Faulted(ex);
        }
    }
}