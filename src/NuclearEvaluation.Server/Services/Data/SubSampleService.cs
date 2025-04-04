using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Interfaces.Data;
using NuclearEvaluation.Server.Services.DB;

namespace NuclearEvaluation.Server.Services.Data;

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