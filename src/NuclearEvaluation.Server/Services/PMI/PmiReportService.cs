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
        // The inline view mapping functions; however, EF generates a single LEFT JOIN that leads to a Cartesian explosion.
        // IncludeOptimized cannot be applied; includes, such as PmiReportDistributionEntries, are always eagerly loaded.
        // Moreover, manual mapping of parent-child relationships is necessary.
        // Using the SQL view (e.g. [DATA].ApmView) avoids these issues.
        IQueryable<PmiReportView> query = _dbContext.PmiReport
            .AsNoTracking()
            .Select(r => new PmiReportView
            {
                Id = r.Id,
                ReportName = r.Name,
                DateUploaded = r.CreatedDate,
                UserName = r.Author.UserName!,
                ReportStatus = r.Status,
                DistributionEntries = r.PmiReportDistributionEntries
                    .Select(de => new PmiReportDistributionEntryView
                    {
                        Id = de.Id,
                        PmiReportId = de.PmiReportId,
                        DistributionChannel = de.DistributionChannel,
                        DistributionStatus = de.DistributionStatus,
                    })
                    .ToList()
            });

        foreach (PmiReportView parent in query)
        {
            foreach (PmiReportDistributionEntryView child in parent.DistributionEntries)
            {
                child.PmiReport = parent;
            }
        }

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