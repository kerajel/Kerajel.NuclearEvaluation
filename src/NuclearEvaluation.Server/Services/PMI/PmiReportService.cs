using Kerajel.Primitives.Models;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Helpers;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Views;
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

    public async Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission, CancellationToken ct)
    {
        try
        {
            PmiReport pmiReport = PreparePmiReport(reportSubmission);
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

            await _dbContext.PmiReportDistributionEntry.Where(x => x.PmiReportId == pmiReportId)
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
        Func<IQueryable<PmiReportView>, IQueryable<PmiReportView>> distributionInclude = (IQueryable<PmiReportView> query) => query.Include(x => x.DistributionEntries);
        Func<IQueryable<PmiReportView>, IQueryable<PmiReportView>> fileMetadataInclude = (IQueryable<PmiReportView> query) => query.Include(x => x.FileMetadata);

        IQueryable<PmiReportView> query = _dbContext.PmiReportView.AsNoTracking();
        query = distributionInclude(query);
        query = fileMetadataInclude(query);

        return ExecuteQuery(query, command, ct);
    }


    private PmiReport PreparePmiReport(PmiReportSubmission reportSubmission)
    {
        PmiReport pmiReport = new()
        {
            Name = reportSubmission.ReportName,
            AuthorId = reportSubmission.AuthorId,
            Author = null!,
            CreatedDate = reportSubmission.ReportDate!.Value,
            Status = PmiReportStatus.Uploaded,
        };

        //OPTIONAL add options to control which channels are active
        foreach (PmiReportDistributionChannel channel in Enum.GetValues<PmiReportDistributionChannel>())
        {
            PmiReportDistributionEntry entry = new()
            {
                PmiReport = pmiReport,
                DistributionChannel = channel,
                DistributionStatus = PmiReportDistributionStatus.Pending,
            };
            pmiReport.PmiReportDistributionEntries.Add(entry);
        }

        PmiReportFileMetadata fileMetadata = new()
        {
            Id = _guidProvider.NewGuid(),
            PmiReport = pmiReport,
            Size = reportSubmission.FileStream.Length,
            FileName = reportSubmission.FileName,
        };

        pmiReport.PmiReportFileMetadata = fileMetadata;

        return pmiReport;
    }
}