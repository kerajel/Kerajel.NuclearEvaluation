using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Shared.Services;

public class PmiReportService : DbServiceBase, IPmiReportService
{
    public PmiReportService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {

    }

    public async Task Insert(PmiReport pmiReport)
    {
        _dbContext.Add(pmiReport);
        await _dbContext.SaveChangesAsync();
    }
}
