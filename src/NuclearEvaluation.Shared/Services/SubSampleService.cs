using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Shared.Services;

public class SubSampleService : DbServiceBase, ISubSampleService
{
    readonly ILogger<SubSampleService> _logger;

    public SubSampleService(
        NuclearEvaluationServerDbContext _dbContext,
        ILogger<SubSampleService> logger) : base(_dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<SubSampleView>> GetSubSampleViews(FetchDataCommand<SubSampleView> command)
    {
        try
        {
            IQueryable<SubSampleView> baseQuery = _dbContext.SubSampleView;
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "");
            return FetchDataResult<SubSampleView>.Faulted(ex);
        }

    }
}