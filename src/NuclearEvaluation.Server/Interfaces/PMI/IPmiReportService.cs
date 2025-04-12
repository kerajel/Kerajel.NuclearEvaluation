using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.PMI;

public interface IPmiReportService
{
    Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission, CancellationToken ct);
    Task<OperationResult> Delete(Guid pmiReportId, CancellationToken ct);
    Task<FetchDataResult<PmiReportView>> GetPmiReportViews(FetchDataCommand<PmiReportView> command, CancellationToken ct = default);
}