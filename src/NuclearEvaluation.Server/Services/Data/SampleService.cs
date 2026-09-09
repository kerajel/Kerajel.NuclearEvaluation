using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Data;

public class SampleService : DbServiceBase, ISampleService
{
    readonly ILogger<SampleService> _logger;

    public SampleService(NuclearEvaluationServerDbContext _dbContext, ILogger<SampleService> logger)
        : base(_dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<SampleView>> GetSampleViews(
        FetchDataCommand<SampleView> command
    )
    {
        try
        {
            IQueryable<SampleView> baseQuery = _dbContext.SampleView;
            int? projectId = command.Query?.ProjectId;
            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(x =>
                    x.Series.ProjectSeries.Any(s => s.ProjectId == projectId.Value)
                );
            }
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "");
            return FetchDataResult<SampleView>.Faulted(ex);
        }
    }
}
