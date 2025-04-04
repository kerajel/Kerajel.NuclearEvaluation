using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Server.Interfaces.PMI;

public interface IPmiReportService
{
    Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission, CancellationToken ct);
    Task<OperationResult> Delete(Guid pmiReportId, CancellationToken ct);
}