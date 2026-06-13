using Kerajel.Primitives.Helpers;
using Kerajel.Primitives.Models;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Shared.Models.Views;
using System.Transactions;

namespace NuclearEvaluation.Server.Services.PMI;

public class PmiReportService : DbServiceBase, IPmiReportService
{
    readonly IGuidProvider _guidProvider;

    public PmiReportService(
        NuclearEvaluationServerDbContext dbContext, IGuidProvider guidProvider) : base(dbContext)
    {
        _guidProvider = guidProvider;
    }

    public async Task<OperationResult<PmiReport>> Create(string reportName, DateOnly reportDate, string fileName, long fileSize, CancellationToken ct)
    {
        try
        {
            PmiReport pmiReport = new()
            {
                Id = _guidProvider.NewGuid(),
                Name = reportName,
                CreatedDate = reportDate,
            };

            pmiReport.PmiReportFileMetadata = new PmiReportFileMetadata
            {
                Id = _guidProvider.NewGuid(),
                PmiReport = pmiReport,
                Size = fileSize,
                FileName = fileName,
            };

            _dbContext.Add(pmiReport);
            await _dbContext.SaveChangesAsync(ct);
            return OperationResult<PmiReport>.Succeeded(pmiReport);
        }
        catch (Exception ex)
        {
            return OperationResult<PmiReport>.Faulted(ex);
        }
    }

    public async Task<OperationResult> Delete(Guid pmiReportId, CancellationToken ct)
    {
        try
        {
            using TransactionScope ts = TransactionProvider.CreateScope();

            await _dbContext.Set<PmiReportFileMetadata>().Where(x => x.PmiReportId == pmiReportId)
                .DeleteAsync(ct);
            await _dbContext.PmiReport.Where(x => x.Id == pmiReportId)
                .DeleteAsync(ct);

            ts.Complete();
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            return OperationResult.Faulted(ex);
        }
    }

    public Task<FetchDataResult<PmiReportView>> GetPmiReportViews(FetchDataCommand<PmiReportView> command, CancellationToken ct = default)
    {
        IQueryable<PmiReportView> query = _dbContext.PmiReportView.AsNoTracking()
            .Include(x => x.FileMetadata);

        return ExecuteQuery(query, command, ct);
    }

    public async Task<bool> IsNameAvailable(string reportName, CancellationToken ct = default)
    {
        bool exists = await EntityFrameworkQueryableExtensions.AnyAsync(_dbContext.PmiReport, x => x.Name == reportName, ct);
        return !exists;
    }
}
